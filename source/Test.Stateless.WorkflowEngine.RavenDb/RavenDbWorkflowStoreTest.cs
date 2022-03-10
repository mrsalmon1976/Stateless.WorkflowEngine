using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Raven.Client;
using Raven.Client.Extensions;
using Raven.Client.Embedded;
using System.IO;
using Raven.Client.Document;
using Stateless.WorkflowEngine.Models;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
using Test.Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.RavenDb;
using Raven.Abstractions.Indexing;

namespace Test.Stateless.WorkflowEngine.RavenDb
{
    /// <summary>
    /// Test fixture for RavenDbWorkflowStore.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    [Category("RequiresRavenDb")]
    public class RavenDbWorkflowStoreTest : WorkflowStoreTestBase
    {

        private EmbeddableDocumentStore _documentStore;

        #region SetUp and TearDown

        [OneTimeSetUp]
        public void RavenDbWorkflowStoreTest_OneTimeSetUp()
        {
            _documentStore = new EmbeddableDocumentStore
            {
                RunInMemory = true,
                UseEmbeddedHttpServer = true,
            };
            _documentStore.Configuration.Storage.Voron.AllowOn32Bits = true;
            _documentStore.Initialize();

        }

        [OneTimeTearDown]
        public void RavenDbWorkflowStoreTest_OneTimeTearDown()
        {
            _documentStore.Dispose();
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

            // remove any indexes
            var indexes = _documentStore.DatabaseCommands.GetIndexes(0, 100);
            foreach (IndexDefinition index in indexes)
            {
                _documentStore.DatabaseCommands.DeleteIndex(index.Name);
            }
        }

        #endregion

        #region RavenDb-specific Tests

        [Test]
        public void Initialise_AutoCreateIndexFalse_IndexesAreNotCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, false);

                var indexes = _documentStore.DatabaseCommands.GetIndexes(0, 100).Where(x => !x.Name.StartsWith("Auto"));
                Assert.AreEqual(0, indexes.Count());
            }
        }

        [Test]
        public void Initialise_AutoCreateIndexTrue_WorkflowIndexesAreCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, true);

                var indexes = _documentStore.DatabaseCommands.GetIndexes(0, 100).Where(x => !x.Name.StartsWith("Auto"));
                Assert.Greater(indexes.Count(), 0);

                var expectedIndex = indexes.FirstOrDefault(x => x.Name == "WorkflowIndex/Priority/RetryCount/CreatedOn");
                Assert.IsNotNull(expectedIndex);
            }
        }

        [Test]
        public void Initialise_AutoCreateIndexTrue_CompletedWorkflowIndexesAreCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, true);

                var indexes = _documentStore.DatabaseCommands.GetIndexes(0, 100).Where(x => !x.Name.StartsWith("Auto"));
                Assert.Greater(indexes.Count(), 0);

                var expectedIndex = indexes.FirstOrDefault(x => x.Name == "CompletedWorkflowIndex/CreatedOn");
                Assert.IsNotNull(expectedIndex);
            }
        }

        #endregion

        #region Private Methods

        private void ClearTestData()
        {
            using (IDocumentSession session = _documentStore.OpenSession())
            {
                // drop all workflows
                var xx = session.Query<WorkflowContainer>().Take(1000);
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
            return new RavenDbWorkflowStore(_documentStore, null);
        }

        #endregion

    }
}
