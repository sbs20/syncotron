﻿using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    /// <summary>
    /// This class mediates all interaction with the local file system. It is required
    /// in order to 
    ///   a) Use a common FileItem class
    ///   b) Log all changes to a local database in order to derives changes
    /// </summary>
    public class LocalFilesystemService : IFileItemProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalFilesystemService));
        private ReplicatorContext context;
        private IHashProvider hashProvider;

        [Serializable]
        private class Cursor
        {
            public string Path { get; set; }
            public bool Recursive { get; set; }
            public bool Deleted { get; set; }

            public override string ToString()
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(memoryStream, this);
                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }

            public static Cursor FromString(string s)
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(s);
                    BinaryFormatter formatter = new BinaryFormatter();
                    using (MemoryStream memoryStream = new MemoryStream(bytes))
                    {
                        return formatter.Deserialize(memoryStream) as Cursor;
                    }
                }
                catch
                {
                    throw new InvalidOperationException("Invalid cursor string");
                }
            }
        }

        public LocalFilesystemService(ReplicatorContext context)
        {
            this.context = context;
        }

        public IHashProvider HashProvider
        {
            get
            {
                if (this.hashProvider == null)
                {
                    switch (this.context.HashProviderType)
                    {
                        case HashProviderType.MD5:
                            this.hashProvider = new MD5Hash();
                            break;

                        case HashProviderType.DateTimeAndSize:
                        default:
                            this.hashProvider = new DateTimeSizeHash();
                            break;
                    }
                }

                return this.hashProvider;
            }
        }

        public FileSystemInfo ToFileSystemInfo(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo file = new FileInfo(path);
            return dir.Exists ? (FileSystemInfo)dir : file;
        }

        public FileItem ToFileItem(string path)
        {
            var fsi = this.ToFileSystemInfo(path);
            if (fsi is FileInfo)
            {
                return FileItem.Create(fsi as FileInfo, this.HashProvider);
            }

            return FileItem.Create(fsi as DirectoryInfo);
        }

        private void FileItemMergeFromIndex(FileItem fileItem)
        {
            var existing = this.context.LocalStorage.IndexSelect(fileItem);
            if (existing != null)
            {
                fileItem.ServerRev = existing.ServerRev;
            }
        }

        public Task DeleteAsync(string path)
        {
            var fsi = this.ToFileSystemInfo(path);
            if (fsi.Exists)
            {
                fsi.Delete();
            }

            return Task.FromResult(0);
        }

        public async Task<string> ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            this.context.LocalStorage.ScanDelete();

            Action<FileItem> internalAction = (fileItem) =>
            {
                log.DebugFormat("ForEachAsync():{0}", fileItem.Path);
                this.FileItemMergeFromIndex(fileItem);

                // Store in scan
                this.context.LocalStorage.ScanInsert(fileItem);
                action(fileItem);
            };

            // This improves performance so much it's unreal
            // see: http://stackoverflow.com/questions/1711631/improve-insert-per-second-performance-of-sqlite
            this.context.LocalStorage.BeginTransaction();

            await Task.Run(() =>
            {
                DirectoryInfo root = new DirectoryInfo(path);
                if (!root.Exists)
                {
                    return;
                }

                SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                foreach (var f in root.EnumerateFiles("*", option))
                {
                    var item = FileItem.Create(f, this.HashProvider);
                    internalAction(item);
                }

                foreach (var d in root.EnumerateDirectories("*", option))
                {
                    var item = FileItem.Create(d);
                    internalAction(item);
                }
            });

            this.context.LocalStorage.EndTransaction();

            return new Cursor
            {
                Path = path,
                Recursive = recursive,
                Deleted = deleted
            }.ToString();
        }

        public async Task<string> ForEachContinueAsync(string cursor, Action<FileItem> action)
        {
            var cursorObj = Cursor.FromString(cursor);
            await this.ForEachAsync(cursorObj.Path, cursorObj.Recursive, cursorObj.Deleted, (x) => { });
            await Task.Run(() =>
            {
                var changes = this.context.LocalStorage.Changes(cursorObj.Path, cursorObj.Recursive, cursorObj.Deleted);
                foreach (var f in changes)
                {
                    action(f);
                }
            });

            return cursor;
        }

        public Task MoveAsync(FileItem file, string desiredPath)
        {
            throw new NotImplementedException();
        }

        public void CreateDirectory(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                dir.Create();
            }
        }

        public async Task WriteAsync(string path, Stream stream, DateTime lastModified)
        {
            var localFile = new FileInfo(path);
            using (Stream localStream = localFile.OpenWrite())
            {
                byte[] buffer = new byte[1 << 16];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await localStream.WriteAsync(buffer, 0, read);
                }
            }

            bool success = false;
            while (!success)
            {
                try
                {
                    localFile.LastWriteTimeUtc = lastModified;
                    success = true;
                }
                catch { await Task.Delay(50); }
            }

            localFile.Refresh();
        }

        public void Certify(IEnumerable<SyncAction> actions, bool strict)
        {
            int errorCount = 0;

            // Check that all file pairs have local and remote version
            foreach (var action in actions)
            {
                if (action.Local == null)
                {
                    if (strict)
                    {
                        log.Error(action.Key + " has no local version. Cannot certify");
                        ++errorCount;
                    }
                }
                else if (action.Remote == null)
                {
                    if (strict)
                    {
                        log.Error(action.Key + " has no remote version. Cannot certify");
                        ++errorCount;
                    }
                }
                else if (action.Local.Size != action.Remote.Size)
                {
                    if (strict)
                    {
                        log.Error(action.Key + " local and remote have different sizes. Cannot certify");
                        ++errorCount;
                    }
                }
                else if (action.Local.ClientModified != action.Remote.ClientModified)
                {
                    log.Debug(action.Key + " local and remote have different modification dates.");
                }
            }

            if (errorCount > 0)
            {
                throw new InvalidOperationException("Errors found. Cannot certify");
            }

            this.context.LocalStorage.BeginTransaction();
            this.context.LocalStorage.UpdateIndexFromScan();

            int certifiableCount = 0;
            foreach (var action in actions)
            {
                if (action.Local != null && action.Remote != null && action.Local.Size == action.Remote.Size)
                {
                    this.context.LocalStorage.IndexUpdate(action.Local, action.Local.Hash, action.Remote.ServerRev);
                    ++certifiableCount;
                }
            }

            log.InfoFormat("Certified {0} matches", certifiableCount);
            this.context.LocalStorage.EndTransaction();
        }

        public Task<string> LatestCursorAsync(string path, bool recursive, bool deleted)
        {
            string cursor = new Cursor
            {
                Path = this.context.LocalPath,
                Deleted = true,
                Recursive = true
            }.ToString();

            return Task.FromResult(cursor);
        }

        public Task<FileItem> FileSelectAsync(string path)
        {
            var fileItem = this.ToFileItem(path);
            this.FileItemMergeFromIndex(fileItem);
            return Task.FromResult(fileItem);
        }
    }
}
