using Sbs20.Data;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Sbs20.Syncotron
{
    internal class LocalFilesystemDb
    {
        const string connectionString = "URI=file:SqliteTest.db";
        private MonoSqliteController dbController;
        private ReplicatorContext context;

        public LocalFilesystemDb(ReplicatorContext context)
        {
            this.dbController = new MonoSqliteController(connectionString);
            this.context = context;
            this.TableCreate();
        }

        private void TableCreate()
        {
            this.dbController.ExecuteNonQuery(@"drop table if exists Scan;");

            this.dbController.ExecuteNonQuery(@"create table if not exists Scan (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");

            this.dbController.ExecuteNonQuery(@"create table if not exists Files (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");
        }

        public void ScanDelete()
        {
            string sql = "delete from Scan;";
            this.dbController.ExecuteNonQuery(sql);
        }

        public void ScanInsert(FileItem fileItem)
        {
            string sql = string.Format("insert into Scan values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                DbController.ToParameter(fileItem.Path),
                DbController.ToParameter(fileItem.Rev),
                DbController.ToParameter((string)null),
                DbController.ToParameter(fileItem.Size),
                DbController.ToParameter(fileItem.IsFolder),
                DbController.ToParameter(fileItem.LastModified),
                DbController.ToParameter(fileItem.ClientModified));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void FileInsert(FileItem fileItem, string serverRev)
        {
            string sql = string.Format("delete from Files where Path={0}; insert into Files values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                DbController.ToParameter(fileItem.Path),
                DbController.ToParameter(fileItem.Rev),
                DbController.ToParameter(serverRev),
                DbController.ToParameter(fileItem.Size),
                DbController.ToParameter(fileItem.IsFolder),
                DbController.ToParameter(fileItem.LastModified),
                DbController.ToParameter(fileItem.ClientModified));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void FileInsert(FileItem fileItem)
        {
            this.FileInsert(fileItem, string.Empty);
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

        public IEnumerable<FileItem> Changes()
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
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public IEnumerable<FileItem> ScanRead()
        {
            string sql = @"select * from Scan";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }
    }
}
