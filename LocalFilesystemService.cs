﻿using Sbs20.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    /// <summary>
    /// This class mediates all interaction with the local file system. It is required
    /// in order to 
    ///   a) Use a common FileItem class
    ///   b) Log all changes to a local database in order to derives changes
    ///   
    /// There should only ever be ONE of these objects in play at a time in order to
    /// avoid SQLite concurrency issues
    /// </summary>
    public class LocalFilesystemService : IFileItemProvider
    {
        private ReplicatorContext context;
        private LocalFilesystemDb database;

        public LocalFilesystemService(ReplicatorContext context)
        {
            this.context = context;
            this.database = new LocalFilesystemDb(context);
        }

        public Task DeleteAsync(FileItem file)
        {
            return Task.Run(() =>
            {
                var localItem = file.Object as FileSystemInfo;
                localItem.Delete();
            });
        }

        public async Task ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            this.database.ScanDelete();

            Action<FileItem> internalAction = (fileItem) =>
            {
                this.database.ScanInsert(fileItem);
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

            this.database.FileInsert(FileItem.Create(dir));
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
            this.database.FileInsert(item, serverRev);
        }
    }
}
