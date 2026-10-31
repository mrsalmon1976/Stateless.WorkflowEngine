using System;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    internal static class TestSettings
    {
        /// <summary>
        /// MongoDB connection string - defaults to the container started by start.ps1 / docker-compose.yml.
        /// </summary>
        public static readonly string MongoDbConnectionString = Environment.GetEnvironmentVariable("STATELESS_MONGODB_CONNECTIONSTRING") ?? "mongodb://localhost:27017";
    }
}
