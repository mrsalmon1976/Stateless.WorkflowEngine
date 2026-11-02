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
using Newtonsoft.Json.Linq;
using Test.Stateless.WorkflowEngine.Workflows.Basic;

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
                Urls = new String[] { TestSettings.RavenDbUrl },
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
                Assert.That(indexNames.Count(), Is.EqualTo(0));
            }
        }

        [Test]
        public void Initialise_AutoCreateIndexTrue_NoStaticIndexesAreCreated()
        {
            // the store relies on RavenDB auto indexes - see RavenDbWorkflowStore.Initialise
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            workflowStore.Initialise(false, true, false);

            string[] indexNames = _documentStore.Maintenance.Send(new GetIndexNamesOperation(0, 10)).Where(x => !x.StartsWith("Auto/")).ToArray();
            Assert.That(indexNames, Is.Empty);
        }

        [Test]
        public void GetSuspendedCount_IsAnsweredFromAutoIndex()
        {
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            workflowStore.Initialise(false, true, false);

            workflowStore.GetSuspendedCount();
            workflowStore.GetActiveCount();

            string[] indexNames = _documentStore.Maintenance.Send(new GetIndexNamesOperation(0, 10));
            Assert.That(indexNames, Does.Contain("Auto/RavenWorkflows/ByWorkflow.IsSuspended"));
        }

        [Test]
        public void GetWorkflowAsJson_ReturnsRawDocument()
        {
            // the web console reads the raw document as it doesn't have the workflow types loaded
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflowStore.Save(wf);

            JObject doc = JObject.Parse(workflowStore.GetWorkflowAsJson(wf.Id));

            Assert.That(doc["WorkflowType"].Value<string>(), Is.EqualTo(typeof(BasicWorkflow).AssemblyQualifiedName));
            Assert.That(doc["Workflow"]["Id"].Value<string>(), Is.EqualTo(wf.Id.ToString()));
            Assert.That(doc["@metadata"]["@id"].Value<string>(), Is.EqualTo($"Workflow/{wf.Id}"));
        }

        [Test]
        public void ClaimNext_DocumentWithoutLockFields_WorkflowClaimed()
        {
            // documents saved before multi-node support don't have the lock fields at all
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            workflowStore.Save(wf);
            _documentStore.Operations.Send(new PatchOperation($"Workflow/{wf.Id}", null, new Raven.Client.Documents.Operations.PatchRequest
            {
                Script = "delete this.Workflow.LockedBy; delete this.Workflow.LockedOn;"
            }));
            JObject doc = JObject.Parse(workflowStore.GetWorkflowAsJson(wf.Id));
            Assert.That(((JObject)doc["Workflow"]).ContainsKey("LockedBy"), Is.False);

            Assert.That(workflowStore.ClaimNext("node1")?.Id, Is.EqualTo(wf.Id));
        }

        [Test]
        public void ClaimNext_WorkflowNotYetDue_PatchDoesNotClaim()
        {
            // the candidate query can be stale, so the claim patch itself checks the workflow is still due
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            wf.ResumeOn = DateTime.UtcNow.AddMinutes(5);
            workflowStore.Save(wf);

            Assert.That(workflowStore.ClaimNext("node1"), Is.Null);
            Assert.That(workflowStore.GetLockOwners(), Is.Empty);
        }

        [Test]
        public void RegisterNode_NodeStoredInWorkflowNodesCollection()
        {
            RavenDbWorkflowStore workflowStore = (RavenDbWorkflowStore)GetStore();
            workflowStore.RegisterNode(new global::Stateless.WorkflowEngine.WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });

            using (var session = _documentStore.OpenSession())
            {
                var node = session.Load<global::Stateless.WorkflowEngine.WorkflowNode>("node1");
                Assert.That(node, Is.Not.Null);
                Assert.That(session.Advanced.GetMetadataFor(node)["@collection"], Is.EqualTo("WorkflowNodes"));
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// The JSON methods return the raw RavenWorkflow document, which wraps the workflow.
        /// </summary>
        protected override T DeserializeJsonWorkflow<T>(string json)
        {
            return JObject.Parse(json)["Workflow"].ToObject<T>();
        }

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
