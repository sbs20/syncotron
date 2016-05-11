using Sbs20.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Sbs20.Syncotron
{
    /// <summary>
    /// There should only ever be ONE of these objects in play at a time in order to
    /// avoid SQLite concurrency issues
    /// </summary>
    public class LocalStorage
    {
        const string connectionString = "URI=file:SqliteTest.db";
        private MonoSqliteController dbController;
        private ReplicatorContext context;

        public LocalStorage(ReplicatorContext context)
        {
            this.dbController = new MonoSqliteController(connectionString);
            this.context = context;
            this.StructureCreate();
        }

        private void StructureCreate()
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

            this.dbController.ExecuteNonQuery(@"create table if not exists Settings (
                    Key varchar(32),
                    Value varchar(2048)
                );");
        }

        public T SettingsRead<T>(string key)
        {
            string sql = string.Format("select Value from Settings where key={0}", DbController.ToParameter(key));
            object o = this.dbController.ExecuteAsScalar(sql);
            try
            {
                return (T)Convert.ChangeType(o, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }

        public void SettingsWrite<T>(string key, T value)
        {
            string val = null;
            if (value != null)
            {
                if (value is DateTime)
                {
                    val = ((DateTime)(value as object)).ToString("o");
                }
                else
                {
                    val = value.ToString();
                }
            }

            string sql = string.Format("delete from Settings where key={0}; insert into Settings values ({0}, {1});", 
                DbController.ToParameter(key),
                DbController.ToParameter(val));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void ScanDelete()
        {
            string sql = "delete from Scan;";
            this.dbController.ExecuteNonQuery(sql);
        }

        public void UpdateFilesFromScan()
        {
            string sql = "delete from Files; insert into Files select * from Scan;";
            this.dbController.ExecuteNonQuery(sql);
        }

        public void ScanInsert(FileItem fileItem)
        {
            string sql = string.Format("insert into Scan values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                DbController.ToParameter(fileItem.Path),
                DbController.ToParameter(fileItem.Hash),
                DbController.ToParameter(fileItem.ServerRev),
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
                DbController.ToParameter(fileItem.Hash),
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

        public void FileUpdate(FileItem fileItemKey, string hash, string serverRev)
        {
            string sql = string.Format("update Files set LocalRev={1}, ServerRev={2} where Path={0};",
                DbController.ToParameter(fileItemKey.Path),
                DbController.ToParameter(fileItemKey.Hash),
                DbController.ToParameter(serverRev));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void FileDelete(string path)
        {
            string sql = string.Format("delete from Files where path={0};", DbController.ToParameter(path));
            this.dbController.ExecuteNonQuery(sql);
        }

        private static FileItem ToFileItem(DataRow r)
        {
            string path = DbController.ToString(r["Path"]);
            bool isDeleted = false;
            try { isDeleted = DbController.ToString(r["Action"]) == "Delete"; }
            catch { }

            return new FileItem
            {
                Path = path,
                ServerRev = DbController.ToString(r["ServerRev"]),
                Hash = DbController.ToString(r["LocalRev"]),
                IsDeleted = isDeleted,
                IsFolder = DbController.ToBoolean(r["IsFolder"]),
                Size = (ulong)DbController.ToInt64(r["Size"]),
                LastModified = DbController.ToDateTime(r["LastModified"]),
                ClientModified = DbController.ToDateTime(r["ClientModified"])
            };
        }

        public IEnumerable<FileItem> Changes(string path, bool recursive, bool deleted)
        {
            // TODO - parameters
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
    where Files.LocalRev != Scan.LocalRev";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public IEnumerable<FileItem> ScanSelect()
        {
            string sql = @"select * from Scan";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public FileItem FileSelect(FileItem keyFile)
        {
            string sql = string.Format("select * from Files where Path = {0} and LocalRev={1};",
                DbController.ToParameter(keyFile.Path),
                DbController.ToParameter(keyFile.Hash));

            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r)).FirstOrDefault();
        }
    }
}
