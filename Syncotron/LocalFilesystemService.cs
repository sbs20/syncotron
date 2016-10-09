using log4net;
using Sbs20.Extensions;
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
        private FileItemIndex index;

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

        public FileItemIndex Index
        {
            get
            {
                if (this.index == null)
                {
                    log.Info("Retrieve file list : start");
                    var files = this.context.LocalStorage.IndexSelect();
                    log.Info("Retrieve file list : finish");
                    this.index = new FileItemIndex(files);
                }

                return this.index;
            }
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
            if (!fsi.Exists)
            {
                return null;
            }

            if (fsi is FileInfo)
            {
                return FileItem.Create(fsi as FileInfo, this.HashProvider);
            }

            return FileItem.Create(fsi as DirectoryInfo);
        }

        private void FileItemMergeFromIndex(FileItem fileItem)
        {
            var existing = this.Index[fileItem.Path];
            if (existing != null && existing.Hash == fileItem.Hash)
            {
                fileItem.ServerRev = existing.ServerRev;
            }
        }

        private void FileItemMergeFromStorage(FileItem fileItem)
        {
            var existing = this.context.LocalStorage.IndexSelect(fileItem);
            if (existing != null && existing.Hash == fileItem.Hash)
            {
                fileItem.ServerRev = existing.ServerRev;
            }
        }

        public Task DeleteAsync(string path)
        {
            var fsi = this.ToFileSystemInfo(path);
            if (fsi.Exists)
            {
                if (fsi is DirectoryInfo)
                {
                    DirectoryInfo dir = fsi as DirectoryInfo;
                    dir.Delete(true);
                }
                else
                {
                    fsi.Delete();
                }
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

        public async Task WriteAsync(string path, ulong size, Stream stream, DateTime lastModified)
        {
            const string suffix = "-syncotron-tmp";
            string temp = path + suffix;

            var tempFile = new FileInfo(temp);
            if (tempFile.Exists)
            {
                tempFile.Delete();
            }

            try
            {
                using (Stream localStream = tempFile.OpenWrite())
                {
                    byte[] buffer = new byte[this.context.HttpChunkSize];
                    int read;
                    ulong offset = 0;
                    DateTime start = DateTime.Now;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)
                        .WithTimeout(TimeSpan.FromSeconds(this.context.HttpReadTimeoutInSeconds))) > 0)
                    {
                        this.ProgressUpdate(path, size, offset += (ulong)read, start);
                        await localStream.WriteAsync(buffer, 0, read);
                    }
                }
            }
            catch (TimeoutException)
            {
                // The file might still be locked. Pause for a moment since this is a
                // fatal error and then try deleting the file.
                await Task.Delay(TimeSpan.FromSeconds(2));
                try { tempFile.Delete(); } catch { }
                throw;
            }

            bool success = false;
            while (!success)
            {
                try
                {
                    tempFile.LastWriteTimeUtc = lastModified;
                    success = true;
                }
                catch { await Task.Delay(50); }
            }

            // We need to wipe out the existing file if it's there
            var localFile = new FileInfo(path);
            if (localFile.Exists)
            {
                localFile.Delete();
            }

            // Move to the correct location
            tempFile.MoveTo(path);
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
                throw new SyncotronException("Errors found. Cannot certify");
            }

            this.context.LocalStorage.BeginTransaction();
            this.context.LocalStorage.UpdateIndexFromScan();
            this.context.LocalStorage.EndTransaction();

            this.context.LocalStorage.BeginTransaction();

            int certifiableCount = 0;
            foreach (var action in actions)
            {
                if (action.Local != null && action.Remote != null && action.Local.Size == action.Remote.Size)
                {
                    this.context.LocalStorage.IndexUpdate(action.Local, action.Local.Hash, action.Remote.ServerRev);
                    action.Local.ServerRev = action.Remote.ServerRev;
                    ++certifiableCount;
                    if (certifiableCount % 1024 == 0)
                    {
                        log.InfoFormat("Certified {0} matches", certifiableCount);
                    }
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
            log.DebugFormat("FileSelectAsync({0})", path);
            var fileItem = this.ToFileItem(path);
            if (fileItem != null)
            {
                this.FileItemMergeFromStorage(fileItem);
            }

            return Task.FromResult(fileItem);
        }

        public void ProgressUpdate(string filepath, ulong filesize, ulong bytes, DateTime start)
        {
            SyncotronMain.TransferProgressWrite(filepath, filesize, bytes, start);
        }
    }
}
