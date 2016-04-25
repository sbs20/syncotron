using Sbs20.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    /// <summary>
    /// TODO Make Singleton
    /// </summary>
    public class LocalFilesystemService : IFileItemProvider
    {
        private class LocalDb
        {
            const string connectionString = "URI=file:SqliteTest.db";
            private static MonoSqlite __sql;

            static LocalDb()
            {
                __sql = new MonoSqlite(connectionString);
                TableCreate();
            }

            public static MonoSqlite Sql
            {
                get { return __sql; }
            }

            private static void TableCreate()
            {
                Sql.ExecuteNonQuery(@"drop table if exists Scan;");

                Sql.ExecuteNonQuery(@"create table if not exists Scan (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");

                Sql.ExecuteNonQuery(@"create table if not exists Files (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");
            }

            public static void ScanDelete()
            {
                string sql = "delete from Scan;";
                Sql.ExecuteNonQuery(sql);
            }

            public static void ScanInsert(FileItem fileItem)
            {
                string sql = string.Format("insert into Scan values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                    DbController.ToParameter(fileItem.Path),
                    DbController.ToParameter(fileItem.Rev),
                    DbController.ToParameter((string)null),
                    DbController.ToParameter(fileItem.Size),
                    DbController.ToParameter(fileItem.IsFolder),
                    DbController.ToParameter(fileItem.LastModified),
                    DbController.ToParameter(fileItem.ClientModified));

                Sql.ExecuteNonQuery(sql);
            }

            public static void FileInsert(FileItem fileItem, string serverRev)
            {
                string sql = string.Format("delete from Files where Path={0}; insert into Files values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                    DbController.ToParameter(fileItem.Path),
                    DbController.ToParameter(fileItem.Rev),
                    DbController.ToParameter(serverRev),
                    DbController.ToParameter(fileItem.Size),
                    DbController.ToParameter(fileItem.IsFolder),
                    DbController.ToParameter(fileItem.LastModified),
                    DbController.ToParameter(fileItem.ClientModified));

                Sql.ExecuteNonQuery(sql);
            }

            public static void FileInsert(FileItem fileItem)
            {
                FileInsert(fileItem, string.Empty);

            }

            private static FileItem ToFileItem(DataRow r)
            {
                string path = DbController.ToString(r["Path"]);
                return new FileItem
                {
                    Path = path,
                    Rev = DbController.ToString(r["LocalRev"]),
                    IsFolder = DbController.ToBoolean(r["IsFolder"]),
                    Size = (ulong)DbController.ToInt64(r["Size"]),
                    LastModified = DbController.ToDateTime(r["LastModified"])
                };
            }

            public static IEnumerable<FileItem> Changes()
            {
                string sql = @"select 'New' Action, *
    from Scan
    where Path not in (select Path from Files)
union
select 'Delete' Action, * 
    from Files
    where Path not in (select Path from Scan)
union
select 
	'Change' Action, scan.*
	from Files
        inner join Scan on Files.Path = Scan.Path
    where Files.LocalRev != Scan.LocalRev or Files.ServerRev != Scan.ServerRev";
                return Sql.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
            }

            public static IEnumerable<FileItem> ScanRead()
            {
                string sql = @"select * from Scan";
                return Sql.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
            }
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
            LocalDb.ScanDelete();

            Action<FileItem> internalAction = (fileItem) =>
            {
                LocalDb.ScanInsert(fileItem);
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
                    var item = FileItem.Create(f);
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

            LocalDb.FileInsert(FileItem.Create(dir));
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
            var item = FileItem.Create(localFile);
            LocalDb.FileInsert(item, serverRev);
        }
    }
}
