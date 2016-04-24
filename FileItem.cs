using Dropbox.Api.Files;
using System;
using System.IO;

namespace Sbs20.Syncotron
{
    public class FileItem
    {
        public bool IsFolder { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public string Id { get; private set; }
        public string Rev { get; private set; }
        public ulong Size { get; private set; }
        public DateTime LastModified { get; private set; }
        public DateTime ClientModified { get; private set; }
        public object Object { get; private set; }
        public bool IsLocal { get; private set; }

        private FileItem()
        {
        }

        public static FileItem Create(FileMetadata dbxfile)
        {
            return new FileItem
            {
                IsFolder = false,
                Name = dbxfile.Name,
                Path = dbxfile.PathDisplay,
                Id = dbxfile.Id,
                Rev = dbxfile.Rev,
                Size = dbxfile.Size,
                LastModified = dbxfile.ServerModified,
                ClientModified = dbxfile.ClientModified,
                Object = dbxfile,
                IsLocal = false
            };
        }

        public static FileItem Create(FolderMetadata dbxfolder)
        {
            return new FileItem
            {
                IsFolder = true,
                Name = dbxfolder.Name,
                Path = dbxfolder.PathDisplay,
                Id = dbxfolder.Id,
                Rev = null,
                Size = 0,
                LastModified = DateTime.MinValue,
                ClientModified = DateTime.MinValue,
                Object = dbxfolder,
                IsLocal = false
            };
        }

        public static FileItem Create(Metadata e)
        {
            if (e is FileMetadata)
            {
                return FileItem.Create(e as FileMetadata);
            }
            else if (e is FolderMetadata)
            {
                return FileItem.Create(e as FolderMetadata);
            }

            throw new InvalidOperationException("Unknown Metadata type");
        }

        public static FileItem Create(FileInfo file)
        {
            return new FileItem
            {
                IsFolder = false,
                Name = file.Name,
                Path = file.FullName.Replace("\\", "/"),
                Id = string.Empty,
                Rev = string.Empty,
                Size = (ulong)file.Length,
                LastModified = file.LastWriteTimeUtc,
                ClientModified = file.LastWriteTimeUtc,
                Object = file,
                IsLocal = true
            };
        }

        public static FileItem Create(DirectoryInfo dir)
        {
            return new FileItem
            {
                IsFolder = false,
                Name = dir.Name,
                Path = dir.FullName.Replace("\\", "/"),
                Id = string.Empty,
                Rev = string.Empty,
                Size = 0,
                LastModified = dir.LastWriteTimeUtc,
                ClientModified = dir.LastWriteTimeUtc,
                Object = dir,
                IsLocal = true
            };
        }
    }
}
