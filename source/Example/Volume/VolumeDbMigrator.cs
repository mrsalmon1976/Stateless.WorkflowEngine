using Dapper;
using Example;
using Example.Shared;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        const string TableMultithreadCreateSqlServer = @"if not exists (select * from sysobjects where name='VolumeTest' and xtype='U')
                create table VolumeTest (
                    Id int not null identity(1,1) primary key,
                    IsProcessed bit NOT NULL,
                    CreateDate datetime NOT NULL,
                    ProcessDate datetime NULL
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
            }
            else if (dbType == ExampleDbType.SqlServer)
            {
                using (var conn = DbHelper.GetConnection(ExampleDbType.SqlServer))
                {
                    conn.Execute(TableMultithreadCreateSqlServer);
                    conn.Execute("TRUNCATE TABLE VolumeTest");
                    conn.Close();
                }
            }

        }
    }
}
