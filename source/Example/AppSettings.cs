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
        public static string SqliteDbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SqliteDbFileName);

        public const string SqliteDbFileName = "StatelessExample.db";

        public static string SqliteConnectionString = String.Format("Data Source={0};Version=3;Journal Mode=WAL;BusyTimeout=5000;", SqliteDbFilePath);


        public const string VolumeExampleMongoDbName = "StatelessVolumeExample";

        /// <summary>
        /// MongoDB connection string - defaults to the container started by start.ps1 / docker-compose.yml.
        /// </summary>
        public static string MongoDbConnectionString = Environment.GetEnvironmentVariable("STATELESS_MONGODB_CONNECTIONSTRING") ?? "mongodb://localhost:27017";

        /// <summary>
        /// RavenDB url - defaults to the container started by start.ps1 / docker-compose.yml.
        /// </summary>
        public static string RavenDbUrl = Environment.GetEnvironmentVariable("STATELESS_RAVENDB_URL") ?? "http://localhost:8080";

        /// <summary>
        /// OTLP (gRPC) endpoint that metrics and traces are exported to - defaults to the Aspire dashboard started by
        /// start.ps1 / docker-compose.yml (view them on http://localhost:18888).
        /// </summary>
        public static string OtlpEndpoint = Environment.GetEnvironmentVariable("STATELESS_OTLP_ENDPOINT") ?? "http://localhost:4317";
    }
}
