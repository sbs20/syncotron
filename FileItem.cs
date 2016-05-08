﻿using Dropbox.Api.Files;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Sbs20.Syncotron
{
    public class FileItem
    {
        public FileService Source { get; set; }
        public bool IsFolder { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Id { get; set; }
        public string ServerRev { get; set; }
        public string Hash { get; set; }
        public ulong Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime ClientModified { get; set; }
        public object Object { get; set; }

        public FileItem()
        {
        }

        public static FileItem Create(FileMetadata dbxFile)
        {
            return new FileItem
            {
                Source = FileService.Dropbox,
                IsFolder = false,
                IsDeleted = dbxFile.IsDeleted,
                Name = dbxFile.Name,
                Path = dbxFile.PathDisplay,
                Id = dbxFile.Id,
                ServerRev = dbxFile.Rev,
                Size = dbxFile.Size,
                LastModified = dbxFile.ServerModified,
                ClientModified = dbxFile.ClientModified,
                Object = dbxFile
            };
        }

        public static FileItem Create(FolderMetadata dbxFolder)
        {
            return new FileItem
            {
                Source = FileService.Dropbox,
                IsFolder = true,
                IsDeleted = dbxFolder.IsDeleted,
                Name = dbxFolder.Name,
                Path = dbxFolder.PathDisplay,
                Id = dbxFolder.Id,
                ServerRev = null,
                Size = 0,
                LastModified = DateTime.MinValue,
                ClientModified = DateTime.MinValue,
                Object = dbxFolder
            };
        }

        public static FileItem Create(DeletedMetadata dbxDeleted)
        {
            return new FileItem
            {
                Source = FileService.Dropbox,
                IsFolder = dbxDeleted.IsFolder,
                IsDeleted = true,
                Name = dbxDeleted.Name,
                Path = dbxDeleted.PathDisplay,
                Id = null,
                ServerRev = null,
                Size = 0,
                LastModified = DateTime.MinValue,
                ClientModified = DateTime.MinValue,
                Object = dbxDeleted
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
            else if (e is DeletedMetadata)
            {
                return FileItem.Create(e as DeletedMetadata);
            }

            throw new InvalidOperationException("Unknown Metadata type");
        }

        public static FileItem Create(FileInfo file, IHashProvider hasher)
        {
            return new FileItem
            {
                Source = FileService.Local,
                IsFolder = false,
                IsDeleted = false,
                Name = file.Name,
                Path = file.FullName.Replace("\\", "/"),
                Id = string.Empty,
                ServerRev = string.Empty,
                Hash = hasher.Hash(file),
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
                IsDeleted = false,
                Name = dir.Name,
                Path = dir.FullName.Replace("\\", "/"),
                Id = string.Empty,
                ServerRev = string.Empty,
                Hash = string.Empty,
                Size = 0,
                LastModified = dir.LastWriteTimeUtc,
                ClientModified = dir.LastWriteTimeUtc,
                Object = dir
            };
        }
    }
}
