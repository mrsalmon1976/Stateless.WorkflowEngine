using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Shared
{
    internal class DbHelper
    {
        public static IDbConnection GetConnection(ExampleDbType dbType)
        {
            IDbConnection? connection = null;
            switch (dbType)
            {
                case ExampleDbType.SqlServer:
                    connection = new SqlConnection(AppSettings.SqlServerConnectionString);
                    break;
                case ExampleDbType.Sqlite:
                    connection = new SQLiteConnection(AppSettings.SqliteConnectionString);
                    break;
                default:
                    throw new NotSupportedException();
            }
            connection.Open();
            return connection;
        }
    }
}
