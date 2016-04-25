using Mono.Data.Sqlite;
using System.Data;
using System.Data.Common;

namespace Sbs20.Data
{
    // http://www.mono-project.com/docs/database-access/providers/sqlite/
    public class MonoSqlite : DbController
    {
        public MonoSqlite(string connectionString) :
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
    }
}
