using System.Data;
using System.Data.SQLite;

namespace Example.Shared
{
    internal class DbHelper
    {
        public static IDbConnection GetConnection(ExampleDbType dbType)
        {
            IDbConnection? connection = null;
            switch (dbType)
            {
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
