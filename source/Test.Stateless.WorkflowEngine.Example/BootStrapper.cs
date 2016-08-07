using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;

namespace Test.Stateless.WorkflowEngine.Example
{
    public class BootStrapper
    {
        public static MongoDbWorkflowStore MongoDbStore()
        {
            var client = new MongoClient("mongodb://localhost");
            var server = client.GetServer();
            var database = server.GetDatabase("StatelessWorkflowTest");
            return new MongoDbWorkflowStore(database);
        }

        public static MemoryWorkflowStore MemoryStore()
        {
            return new MemoryWorkflowStore();
        }

        public static RavenDbWorkflowStore RavenDbEmbeddedStore()
        {
            // configure the document store and the session
            var ds = new EmbeddableDocumentStore { RunInMemory = true };
            ds.Initialize();
            return new RavenDbWorkflowStore(ds, String.Empty);
        }

        public static RavenDbWorkflowStore RavenDbStore()
        {
            // the following is the running server configuration - use this if you want to play around with documents
            const string WorkflowDatabase = "Workflows";

            // configure the document store and the session
            var ds = new DocumentStore { ConnectionStringName = "RavenDb" };
            ds.Initialize();
            ds.DatabaseCommands.GlobalAdmin.EnsureDatabaseExists(WorkflowDatabase);
            return new RavenDbWorkflowStore(ds, WorkflowDatabase);
        }

    }
}
