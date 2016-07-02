using log4net;
using Sbs20.Data;
using Sbs20.Extensions;
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
    public class LocalStorage : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalStorage));

        const string connectionStringStub = "URI=file:{0}";
        private MonoSqliteController dbController;

        public LocalStorage(string filename)
        {
            string connectionString = string.Format(connectionStringStub, filename);
            this.dbController = new MonoSqliteController(connectionString);
            this.dbController.JournalInMemory();
            this.StructureCreate();
        }

        private void ScanTableDrop()
        {
            this.dbController.ExecuteNonQuery(@"drop table if exists scan;");
        }

        private void ScanTableCreate()
        {
            this.dbController.ExecuteNonQuery(@"create table if not exists scan (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");
        }

        private void IndexTableDrop()
        {
            this.dbController.ExecuteNonQuery(@"drop table if exists indx;");
        }

        private void IndexTableCreate()
        {
            this.dbController.ExecuteNonQuery(@"create table if not exists indx (
                    Path nvarchar(1024) collate nocase,
                    LocalRev varchar(64),
                    ServerRev varchar(64),
                    Size long,
                    IsFolder boolean,
                    LastModified varchar(19),
                    ClientModified varchar(19)
                );");
        }

        private void IndexTableIndexCreate()
        {
            this.dbController.ExecuteNonQuery(@"create index if not exists ixpath on indx(Path);");
        }

        private void SettingsTableCreate()
        {
            this.dbController.ExecuteNonQuery(@"create table if not exists settings (
                    Key varchar(32),
                    Value varchar(2048)
                );");
        }

        private void ActionTableCreate()
        {
            this.dbController.ExecuteNonQuery(@"create table if not exists action (
                    Type varchar(32),
                    Path varchar(2048)
                );");
        }

        private void StructureCreate()
        {
            this.ScanTableDrop();
            this.ScanTableCreate();
            this.IndexTableCreate();
            this.SettingsTableCreate();
            this.ActionTableCreate();
        }

        public T SettingsRead<T>(string key)
        {
            string sql = string.Format("select Value from settings where key={0}", DbController.ToParameter(key));
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

            string sql = string.Format("delete from settings where key={0}; insert into settings values ({0}, {1});",
                DbController.ToParameter(key),
                DbController.ToParameter(val));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void ScanDelete()
        {
            string sql = "delete from scan;";
            this.dbController.ExecuteNonQuery(sql);
        }

        public void UpdateIndexFromScan()
        {
            this.IndexTableDrop();
            this.IndexTableCreate();
            string sql = "insert into indx select * from scan;";
            this.dbController.ExecuteNonQuery(sql);
            this.IndexTableIndexCreate();
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

        public void ScanInsert(FileItem fileItem)
        {
            string sql = string.Format("insert into scan values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                DbController.ToParameter(fileItem.Path),
                DbController.ToParameter(fileItem.Hash),
                DbController.ToParameter(fileItem.ServerRev),
                DbController.ToParameter(fileItem.Size),
                DbController.ToParameter(fileItem.IsFolder),
                DbController.ToParameter(fileItem.LastModified),
                DbController.ToParameter(fileItem.ClientModified));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void IndexWrite(FileItem fileItem)
        {
            string sql = string.Format("delete from indx where Path={0}; insert into indx values ({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                DbController.ToParameter(fileItem.Path),
                DbController.ToParameter(fileItem.Hash),
                DbController.ToParameter(fileItem.ServerRev),
                DbController.ToParameter(fileItem.Size),
                DbController.ToParameter(fileItem.IsFolder),
                DbController.ToParameter(fileItem.LastModified),
                DbController.ToParameter(fileItem.ClientModified));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void IndexUpdate(FileItem fileItemKey, string hash, string serverRev)
        {
            string sql = string.Format("update indx set LocalRev={1}, ServerRev={2} where Path={0};",
                DbController.ToParameter(fileItemKey.Path),
                DbController.ToParameter(hash),
                DbController.ToParameter(serverRev));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void IndexDelete(string path)
        {
            string sql = string.Format("delete from indx where path={0};", DbController.ToParameter(path));
            this.dbController.ExecuteNonQuery(sql);
        }

        public FileItem IndexSelect(FileItem keyFile)
        {
            log.DebugFormat("IndexSelect({0})", keyFile.Path);

            string sql = string.Format("select * from indx where Path = {0};",
                DbController.ToParameter(keyFile.Path));

            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r)).FirstOrDefault();
        }

        public IEnumerable<FileItem> IndexSelect()
        {
            string sql = "select * from indx;";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public IEnumerable<FileItem> Changes(string path, bool recursive, bool deleted)
        {
            // TODO - parameters
            string sql = @"select 'New' Action, *
    from scan
    where Path not in (select Path from indx)
union
select 'Delete' Action, * 
    from indx
    where Path not in (select Path from scan)
union
select 
	'Change' Action, scan.*
	from indx
        inner join scan on indx.Path = scan.Path
    where indx.LocalRev != scan.LocalRev";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public IEnumerable<FileItem> ScanSelect()
        {
            string sql = @"select * from scan";
            return this.dbController.ExecuteAsEnumerableRows(sql).Select(r => ToFileItem(r));
        }

        public void ActionDelete(SyncAction action)
        {
            string sql = string.Format(@"delete from action where Type={0} and Path={1};",
                DbController.ToParameter(action.Type.ToString()),
                DbController.ToParameter(action.CommonPath));

            this.dbController.ExecuteNonQuery(sql);
        }

        public void ActionWrite(SyncAction action)
        {
            this.ActionDelete(action);
            string sql = string.Format(@"insert into action values ({0}, {1});",
                DbController.ToParameter(action.Type.ToString()),
                DbController.ToParameter(action.CommonPath));

            this.dbController.ExecuteNonQuery(sql);
        }

        public IEnumerable<SyncAction> ActionSelect()
        {
            string sql = "select * from action";
            return this.dbController
                .ExecuteAsEnumerableRows(sql)
                .Select(r =>
                {
                    return new SyncAction
                    {
                        Type = DbController.ToString(r["Type"]).ToEnum<SyncActionType>(),
                        CommonPath = DbController.ToString(r["Path"])
                    };
                });
        }

        public void BeginTransaction()
        {
            this.dbController.ExecuteNonQuery("BEGIN TRANSACTION");
        }

        public void EndTransaction()
        {
            this.dbController.ExecuteNonQuery("END TRANSACTION");
        }

        public void Dispose()
        {
            if (this.dbController != null)
            {
                this.dbController.Dispose();
            }
        }
    }
}
