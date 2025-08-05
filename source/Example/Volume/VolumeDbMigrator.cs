using Dapper;
using Example;
using Example.Shared;
using System.Data.SQLite;

namespace Stateless.TestHarness.Multithread
{
    internal class VolumeDbMigrator
    {
        const string TableVolumeCreateSqlLite = @"
                CREATE TABLE IF NOT EXISTS VolumeTest (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IsProcessed INTEGER NOT NULL,
                    CreateDate TEXT NOT NULL,
                    ProcessDate TEXT NULL
                    )";

        public static void Run(ExampleDbType dbType)
        {
            if (dbType == ExampleDbType.Sqlite)
            {
                string dbPath = AppSettings.SqliteDbFilePath;

                if (System.IO.File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
                SQLiteConnection.CreateFile(dbPath);

                using (var conn = DbHelper.GetConnection(ExampleDbType.Sqlite))
                {
                    conn.Execute(TableVolumeCreateSqlLite);
                    conn.Execute("DELETE FROM VolumeTest");
                    conn.Close();
                }

                return;
            }

            throw new NotImplementedException($"Unsupported database {dbType.ToString()}");

        }
    }
}
