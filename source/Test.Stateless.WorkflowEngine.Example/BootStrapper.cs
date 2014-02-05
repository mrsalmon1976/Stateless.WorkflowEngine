using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Extensions;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.Example
{
    public class BootStrapper
    {
        public static void Boot()
        {
            WorkflowEngineBootStrapper.Boot();

            //// the following is the in-memory test configuration - this is the one to use usually
            //// configure the document store and the session
            //ObjectFactory.Configure(x => x.ForSingletonOf<IDocumentStore>().Use(() =>
            //{

            //    // default usage: use an in-mrmory database for unit test.  Make sure you apply the ds.Convenstions.DefaultQueryingConsistency 
            //    // line below, otherwise you will randomly get errors when querying the store after a write
            //    var ds = new EmbeddableDocumentStore { RunInMemory = true };
            //    ds.Initialize();
            //    return ds;
            //}));
            //ObjectFactory.Configure(x => x.For<IDocumentSession>().Use(ctx =>
            //{
            //    return ctx.GetInstance<IDocumentStore>().OpenSession();
            //}));

            // the following is the running server configuration - use this if you want to play around with documents
            const string WorkflowDatabase = "Workflows";

            // configure the document store and the session
            ObjectFactory.Configure(x => x.ForSingletonOf<IDocumentStore>().Use(() =>
            {
                var ds = new DocumentStore { ConnectionStringName = "RavenDb" };
                ds.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;
                ds.Initialize();
                ds.DatabaseCommands.EnsureDatabaseExists(WorkflowDatabase);
                return ds;
            }));
            ObjectFactory.Configure(x => x.For<IDocumentSession>().Use(ctx =>
            {
                return ctx.GetInstance<IDocumentStore>().OpenSession(WorkflowDatabase);
            }));
        }
    }
}
