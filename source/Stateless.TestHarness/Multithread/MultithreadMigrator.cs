using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.TestHarness.Multithread
{
    internal class MultithreadMigrator
    {
        const string TableMultithreadCreate = @"if not exists (select * from sysobjects where name='MultithreadTest' and xtype='U')
                create table MultithreadTest (
                    Id int not null identity(1,1) primary key,
                    IsProcessed bit NOT NULL,
                    CreateDate datetime NOT NULL,
                    ProcessDate datetime NULL
                    )";
        public static void Run()
        {
            using (var conn = new SqlConnection(AppSettings.ConnectionString))
            {
                conn.Open();
                conn.Execute(TableMultithreadCreate);
                conn.Execute("TRUNCATE TABLE MultithreadTest");
                conn.Close();
            }

        }
    }
}
