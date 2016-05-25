using Dropbox.Api.Files;
using System.Collections.Generic;

namespace Sbs20.Syncotron.Diagnostics
{
    public class Serialiserabler
    {
        public static object ToSerialisable(FolderMetadata f)
        {
            return new
            {
                Id = f.Id,
                IsDeleted = f.IsDeleted,
                IsFile = f.IsFile,
                IsFolder = f.IsFolder,
                Name = f.Name,
                ParentSharedFolderId = f.ParentSharedFolderId,
                PathDisplay = f.PathDisplay,
                PathLower = f.PathLower,
                SharedFolderId = f.SharedFolderId,
                SharingInfo = f.SharingInfo
            };
        }

        public static object ToSerialisable(FileMetadata f)
        {
            return new
            {
                ClientModified = f.ClientModified,
                Id = f.Id,
                IsDeleted = f.IsDeleted,
                IsFile = f.IsFile,
                IsFolder = f.IsFolder,
                Name = f.Name,
                ParentSharedFolderId = f.ParentSharedFolderId,
                PathDisplay = f.PathDisplay,
                PathLower = f.PathLower,
                Rev = f.Rev,
                ServerModified = f.ServerModified,
                SharingInfo = f.SharingInfo,
                Size = f.Size
            };
        }

        public static object ToSerialisable(Metadata m)
        {
            if (m.AsFile != null)
            {
                return ToSerialisable(m.AsFile);
            }

            if (m.AsFolder != null)
            {
                return ToSerialisable(m.AsFolder);
            }

            return null;
        }

        public static object ToSerialisable(ListFolderResult r)
        {
            IList<object> entries = new List<object>();
            foreach (var e in r.Entries)
            {
                entries.Add(ToSerialisable(e));
            }

            return new
            {
                Cursor = r.Cursor,
                Entries = entries,
                HasMore = r.HasMore
            };
        }

        public static object ToSerialisable(object o)
        {
            if (o is Metadata)
            {
                return ToSerialisable(o as Metadata);
            }

            if (o is ListFolderResult)
            {
                return ToSerialisable(o as ListFolderResult);
            }

            return o;
        }
    }
}
