using Dropbox.Api.Files;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Sbs20.Syncotron
{
    public class FileItem
    {
        public FileService Source { get; set; }
        public bool IsFolder { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Id { get; set; }
        public string Rev { get; set; }
        public ulong Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime ClientModified { get; set; }
        public object Object { get; set; }

        public FileItem()
        {
        }

        private static string GetRev(FileInfo fileInfo)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = fileInfo.OpenRead())
                {
                    var hash = md5.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
        }

        public static FileItem Create(FileMetadata dbxfile)
        {
            return new FileItem
            {
                Source = FileService.Dropbox,
                IsFolder = false,
                Name = dbxfile.Name,
                Path = dbxfile.PathDisplay,
                Id = dbxfile.Id,
                Rev = dbxfile.Rev,
                Size = dbxfile.Size,
                LastModified = dbxfile.ServerModified,
                ClientModified = dbxfile.ClientModified,
                Object = dbxfile
            };
        }

        public static FileItem Create(FolderMetadata dbxfolder)
        {
            return new FileItem
            {
                Source = FileService.Dropbox,
                IsFolder = true,
                Name = dbxfolder.Name,
                Path = dbxfolder.PathDisplay,
                Id = dbxfolder.Id,
                Rev = null,
                Size = 0,
                LastModified = DateTime.MinValue,
                ClientModified = DateTime.MinValue,
                Object = dbxfolder
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
                Source = FileService.Local,
                IsFolder = false,
                Name = file.Name,
                Path = file.FullName.Replace("\\", "/"),
                Id = string.Empty,
                Rev = FileItem.GetRev(file),
                Size = (ulong)file.Length,
                LastModified = file.LastWriteTimeUtc,
                ClientModified = file.LastWriteTimeUtc,
                Object = file
            };
        }

        public static FileItem Create(DirectoryInfo dir)
        {
            return new FileItem
            {
                Source = FileService.Local,
                IsFolder = true,
                Name = dir.Name,
                Path = dir.FullName.Replace("\\", "/"),
                Id = string.Empty,
                Rev = string.Empty,
                Size = 0,
                LastModified = dir.LastWriteTimeUtc,
                ClientModified = dir.LastWriteTimeUtc,
                Object = dir
            };
        }
    }
}
