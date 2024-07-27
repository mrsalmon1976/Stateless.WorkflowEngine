using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example
{
    internal class AppSettings
    {
        public const string SqlServerConnectionString = "Server=localhost;Database=StatelessTest;User Id=dev;Password=dev;TrustServerCertificate=True";

        public static string SqliteDbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SqliteDbFileName);

        public const string SqliteDbFileName = "StatelessExample.db";

        public static string SqliteConnectionString = String.Format("Data Source={0};Version=3;", SqliteDbFilePath);


        public const string VolumeExampleMongoDbName = "StatelessVolumeExample";
    }
}
