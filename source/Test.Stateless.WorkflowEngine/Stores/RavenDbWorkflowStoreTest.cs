using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Raven.Client;
using Raven.Client.Extensions;
using StructureMap;
using Raven.Client.Embedded;
using System.IO;
using Raven.Database.Server;
using Raven.Client.Document;
using Stateless.WorkflowEngine.Models;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;

namespace Test.Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Test fixture for RavenDbWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    public class RavenDbWorkflowStoreTest : WorkflowStoreTestBase
    {

        #region SetUp and TearDown

        [TestFixtureSetUp]
        public void RavenDbWorkflowStoreTest_FixtureSetUp()
        {
            // the following is the in-memory test configuration - this is the one to use usually
            // configure the document store and the session
            ObjectFactory.Configure(x => x.ForSingletonOf<IDocumentStore>().Use(() =>
            {

                // default usage: use an in-mrmory database for unit test.  Make sure you apply the ds.Convenstions.DefaultQueryingConsistency 
                // line below, otherwise you will randomly get errors when querying the store after a write
                var ds = new EmbeddableDocumentStore { RunInMemory = true };
                ds.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;
                ds.Initialize();
                return ds;
            }));
            ObjectFactory.Configure(x => x.For<IDocumentSession>().Use(ctx =>
            {
                return ctx.GetInstance<IDocumentStore>().OpenSession();
            }));


            //// the following is the running server configuration - use this if you want to play around with documents
            //const string WorkflowDatabase = "Workflows";

            //// configure the document store and the session
            //ObjectFactory.Configure(x => x.ForSingletonOf<IDocumentStore>().Use(() =>
            //{
            //    var ds = new DocumentStore { ConnectionStringName = "RavenDb" };
            //    ds.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;
            //    ds.Initialize();
            //    ds.DatabaseCommands.EnsureDatabaseExists(WorkflowDatabase);
            //    return ds;
            //}));
            //ObjectFactory.Configure(x => x.For<IDocumentSession>().Use(ctx =>
            //{
            //    return ctx.GetInstance<IDocumentStore>().OpenSession(WorkflowDatabase);
            //}));

        }

        [TestFixtureTearDown]
        public void RavenDbWorkflowStoreTest_FixtureTearDown()
        {
            ObjectFactory.GetInstance<IDocumentStore>().Dispose();
        }

        [SetUp]
        public void RavenDbWorkflowStoreTest_SetUp()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        [TearDown]
        public void RavenDbWorkflowStoreTest_TearDown()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        #endregion

        #region Private Methods

        private void ClearTestData()
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                // drop all workflows
                IEnumerable<WorkflowContainer> workflows = session.Query<WorkflowContainer>().Take(1000);
                foreach (WorkflowContainer wi in workflows)
                {
                    session.Delete(wi);
                }

                // drop all complete workflows
                IEnumerable<CompletedWorkflow> completedWorkflows = session.Query<CompletedWorkflow>().Take(1000);
                foreach (CompletedWorkflow cw in completedWorkflows)
                {
                    session.Delete(cw);
                }
                session.SaveChanges();
            }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Gets the store relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected override IWorkflowStore GetStore()
        {
            return new RavenDbWorkflowStore();
        }

        #endregion

    }
}
