using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.RavenDb;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions.Database;
using Raven.Client.Exceptions;
using Raven.Client.ServerWide.Operations;
using Raven.Client.ServerWide;
using Raven.Client.Documents.Operations.Indexes;

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

        private IDocumentStore _documentStore;
        private const string DbName = "StatelessWorkflowRavenDbTest";

        #region SetUp and TearDown


        [SetUp]
        public void RavenDbWorkflowStoreTest_SetUp()
        {
            _documentStore = new DocumentStore
            {
                Urls = new String[] { "http://localhost:8080" },
                Database = DbName
            };
            _documentStore.Initialize();

            var result = _documentStore.Maintenance.Server.Send(new GetDatabaseRecordOperation(DbName));
            if (result == null)
            {
                try
                {
                    _documentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(DbName)));
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }
        }

        [TearDown]
        public void RavenDbWorkflowStoreTest_TearDown()
        {
            _documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(DbName, true));
            _documentStore.Dispose();


            //// remove any indexes
            //var indexes = _documentStore.DatabaseCommands.GetIndexes(0, 100);
            //foreach (IndexDefinition index in indexes)
            //{
            //    _documentStore.DatabaseCommands.DeleteIndex(index.Name);
            //}
        }

        #endregion

        #region RavenDb-specific Tests

        [Test]
        public void Initialise_AutoCreateIndexFalse_IndexesAreNotCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, false, false);

                string[] indexNames = _documentStore.Maintenance.Send(new GetIndexNamesOperation(0, 10)).Where(x => !x.StartsWith("Auto")).ToArray();
                Assert.AreEqual(0, indexNames.Count());
            }
        }

        [Test]
        public void Initialise_AutoCreateIndexTrue_WorkflowIndexesAreCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, true, false);

                string[] indexNames = _documentStore.Maintenance.Send(new GetIndexNamesOperation(0, 10)).Where(x => !x.StartsWith("Auto")).ToArray();
                Assert.Greater(indexNames.Count(), 0);

                var expectedIndex = indexNames.FirstOrDefault(x => x == "WorkflowIndex/Priority/RetryCount/CreatedOn");
                Assert.IsNotNull(expectedIndex);
            }
        }

        [Test]
        public void Initialise_AutoCreateIndexTrue_CompletedWorkflowIndexesAreCreated()
        {
            using (var session = _documentStore.OpenSession())
            {
                RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
                workflowStore.Initialise(false, true, false);

                string[] indexNames = _documentStore.Maintenance.Send(new GetIndexNamesOperation(0, 10)).Where(x => !x.StartsWith("Auto")).ToArray();
                Assert.Greater(indexNames.Count(), 0);

                var expectedIndex = indexNames.FirstOrDefault(x => x == "CompletedWorkflowIndex/CreatedOn");
                Assert.IsNotNull(expectedIndex);
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
            return new RavenDbWorkflowStore(_documentStore);
        }

        #endregion

    }
}
