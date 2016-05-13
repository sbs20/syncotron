using Mono.Data.Sqlite;
using System.Data;
using System.Data.Common;

namespace Sbs20.Data
{
    // This class provides a DbController wrapper to the Mono.Data.SqliteClient class
    // http://www.mono-project.com/docs/database-access/providers/sqlite/
    public class MonoSqliteController : DbController
    {
        public MonoSqliteController(string connectionString) :
            base(connectionString)
        { }

        protected override IDbConnection CreateConnection()
        {
            var connection = new SqliteConnection(this.connectionString);
            connection.Open();
            return connection;
        }

        protected override DbDataAdapter CreateDataAdapter(IDbCommand command)
        {
            return new SqliteDataAdapter(command as SqliteCommand);
        }

        public void JournalInMemory()
        {
            this.ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
        }

        public void BeginTransaction()
        {
            this.ExecuteNonQuery("BEGIN TRANSACTION");
        }

        public void EndTransaction()
        {
            this.ExecuteNonQuery("END TRANSACTION");
        }
    }
}
