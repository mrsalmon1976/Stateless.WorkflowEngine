using System;

namespace Test.Stateless.WorkflowEngine.RavenDb
{
    internal static class TestSettings
    {
        /// <summary>
        /// RavenDB url - defaults to the container started by start.ps1 / docker-compose.yml.
        /// </summary>
        public static readonly string RavenDbUrl = Environment.GetEnvironmentVariable("STATELESS_RAVENDB_URL") ?? "http://localhost:8080";
    }
}
