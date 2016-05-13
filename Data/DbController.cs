using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sbs20.Data
{
    public abstract class DbController : IDisposable
    {
        protected int CommmandTimeout { get; set; }
        protected string connectionString;
        private IDbConnection connection;

        protected DbController(string connectionString)
        {
            this.connectionString = connectionString;
        }

        protected abstract IDbConnection CreateConnection();
        protected abstract DbDataAdapter CreateDataAdapter(IDbCommand command);

        private IDbConnection Connection
        {
            get
            {
                if (this.connection == null)
                {
                    this.connection = this.CreateConnection();
                }

                return this.connection;
            }
        }

        private IDbCommand CreateCommand(IDbConnection connection, string commandText)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = CommmandTimeout;
            command.CommandText = commandText;
            return command;
        }

        private object mutex = new object();
        private T ExecuteAs<T>(string commandText, Func<IDbCommand, T> function)
        {
            lock(mutex)
            {
                using (IDbCommand command = CreateCommand(this.Connection, commandText))
                {
                    return function(command);
                }
            }
        }

        public DataTable ExecuteAsTable(string commandText)
        {
            return ExecuteAs<DataTable>(commandText, (command) =>
            {
                DataTable dataTable = new DataTable();
                using (DbDataAdapter dataAdapter = this.CreateDataAdapter(command))
                {
                    dataAdapter.Fill(dataTable);
                }
                return dataTable;
            });
        }

        public int ExecuteNonQuery(string commandText)
        {
            return ExecuteAs<int>(commandText, (command) =>
            {
                return command.ExecuteNonQuery();
            });
        }

        public object ExecuteAsScalar(string commandText)
        {
            return ExecuteAs<object>(commandText, (command) =>
            {
                return command.ExecuteScalar();
            });
        }

        public EnumerableRowCollection<DataRow> ExecuteAsEnumerableRows(string commandText)
        {
            using (DataTable table = this.ExecuteAsTable(commandText))
            {
                return table.AsEnumerable();
            }
        }

        public static string ToParameter(string val, DbTextParameterFlag textFlag)
        {
            string result = "null";
            string unicodeModifier = (textFlag & DbTextParameterFlag.Unicode) != DbTextParameterFlag.None ? "N" : "";
            Func<string, string> transform = (s) =>
            {
                return unicodeModifier + "'" + Regex.Replace(val, "'", "''") + "'";
            };

            Func<string, bool> test = (s) =>
            {
                if ((textFlag & DbTextParameterFlag.AllowEmpty) != DbTextParameterFlag.None)
                {
                    return s != null;
                }

                return !string.IsNullOrEmpty(s);
            };

            if (test(val))
            {
                result = transform(val);
            }

            return result;
        }

        public static string ToParameter(string val)
        {
            return ToParameter(val, DbTextParameterFlag.None);
        }

        public static string ToParameter(double val)
        {
            return val.ToString();
        }

        public static string ToParameter(DateTime val)
        {
            return ToParameter(val.ToString("o"));
        }

        public static string ToParameter(bool val)
        {
            return val ? "1" : "0";
        }

        public static string ToParameter(Enum val)
        {
            return Convert.ToInt32(val).ToString();
        }

        public static string ToParameter(object val)
        {
            if (val is DateTime)
            {
                return ToParameter((DateTime)val);
            }
            else if (val is int || val is double)
            {
                return ToParameter((double)val);
            }
            else if (val is bool)
            {
                return ToParameter((bool)val);
            }
            else if (val is string)
            {
                return ToParameter((string)val);
            }

            throw new InvalidOperationException("Unknown datatype");
        }

        public static string ToString(object o, string def)
        {
            return o is DBNull ? def : Convert.ToString(o);
        }

        public static string ToString(object o)
        {
            return ToString(o, null);
        }

        public static double ToDouble(object o)
        {
            return o is DBNull || (o is string && string.IsNullOrEmpty((string)o)) ? 0 : Convert.ToDouble(o);
        }

        public static int ToInt32(object o)
        {
            return o is DBNull ? 0 : Convert.ToInt32(o);
        }

        public static long ToInt64(object o)
        {
            return o is DBNull ? 0 : Convert.ToInt64(o);
        }

        public static bool ToBoolean(object o)
        {
            return o is DBNull ? false : Convert.ToBoolean(o);
        }

        public static DateTime ToDateTime(object o)
        {
            return o is DBNull ? DateTime.MinValue : Convert.ToDateTime(o);
        }

        public void Dispose()
        {
            if (this.connection != null)
            {
                this.connection.Dispose();
            }
        }
    }
}
