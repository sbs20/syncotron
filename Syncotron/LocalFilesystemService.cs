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
        private ReplicatorContext context;

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

        public Task DeleteAsync(FileItem file)
        {
            return Task.Run(() =>
            {
                var localItem = file.Object as FileSystemInfo;
                localItem.Delete();
            });
        }

        public string DefaultCursor
        {
            get
            {
                return new Cursor
                {
                    Path = this.context.LocalPath,
                    Deleted = true,
                    Recursive = true
                }.ToString();
            }
        }

        public async Task<string> ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            this.context.LocalStorage.ScanDelete();

            Action<FileItem> internalAction = (fileItem) =>
            {
                this.context.LocalStorage.ScanInsert(fileItem);
                action(fileItem);
            };

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
                    var item = FileItem.Create(f, this.context.HashProvider);
                    internalAction(item);
                }

                foreach (var d in root.EnumerateDirectories("*", option))
                {
                    var item = FileItem.Create(d);
                    internalAction(item);
                }
            });

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

            this.context.LocalStorage.FileInsert(FileItem.Create(dir));
        }

        public async Task WriteAsync(string path, Stream stream, string serverRev, DateTime lastModified)
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
            var item = FileItem.Create(localFile, this.context.HashProvider);
            this.context.LocalStorage.FileInsert(item, serverRev);
        }

        public void Certify(IEnumerable<FileItemPair> matches)
        {
            // Check that all file pairs have local and remote version
            foreach (var filePairEntry in matches)
            {
                if (filePairEntry.Local == null)
                {
                    Logger.error(this, filePairEntry.Key + " has no local version. Cannot certify");
                    this.context.LocalStorage.SettingsWrite("IsCertified", false);
                    return;
                }
                else if (filePairEntry.Remote == null)
                {
                    Logger.error(this, filePairEntry.Key + " has no remote version. Cannot certify");
                    this.context.LocalStorage.SettingsWrite("IsCertified", false);
                    return;
                }
                else if (filePairEntry.Local.Size != filePairEntry.Remote.Size)
                {
                    Logger.error(this, filePairEntry.Key + " local and remote have different sizes. Cannot certify");
                    this.context.LocalStorage.SettingsWrite("IsCertified", false);
                    return;
                }
                else if (filePairEntry.Local.ClientModified != filePairEntry.Remote.ClientModified)
                {
                    Logger.warn(this, filePairEntry.Key + " local and remote have different modification dates.");
                }
            }

            this.context.LocalStorage.UpdateFilesFromScan();

            foreach (var match in matches)
            {
                this.context.LocalStorage.FileUpdate(match.Local, match.Local.Hash, match.Remote.ServerRev);
            }

            this.context.LocalStorage.SettingsWrite("IsCertified", true);
        }
    }
}
