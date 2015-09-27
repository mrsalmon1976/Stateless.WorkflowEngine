using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using NUnit.Framework;
using System.IO;
using MongoDB.Driver;
using NSubstitute;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using MongoDB.Bson;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Stateless.WorkflowEngine.UI.Console.AppCode;
using MongoDB.Bson.Serialization;
using Stateless.WorkflowEngine.Models;

namespace Test.Stateless.WorkflowEngine.UI.Console.AppCode.Providers
{
    /// <summary>
    /// Test fixture for MongoDbWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    [Category("RequiresMongoDb")]
    public class MongoDbWorkflowProviderTest
    {

        private WorkflowStoreConnection _conn = null; 
        private MongoDatabase _database = null;
        
        private static string UITestDatabase = "StatelessUITestDb";
        private static string UITestCollection = "StatelessUITestCollection";

        #region SetUp and TearDown

        [TestFixtureSetUp]
        public void MongoDbWorkflowProviderTest_FixtureSetUp()
        {
            var connectionString = "mongodb://localhost";
            var client = new MongoClient(connectionString);
            var server = client.GetServer();
            _database = server.GetDatabase(UITestDatabase);

            _conn = new WorkflowStoreConnection();
            _conn.Host = "localhost";
            _conn.Port = Constants.MongoDbDefaultPort;
            _conn.DatabaseName = UITestDatabase;
            _conn.ActiveCollection = UITestCollection;
            //_conn.CompleteCollection = "CompletedWorkflows";
        }

        [TestFixtureTearDown]
        public void MongoDbWorkflowProviderTest_FixtureTearDown()
        {
        }

        [SetUp]
        public void MongoDbWorkflowProviderTest_SetUp()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        [TearDown]
        public void MongoDbWorkflowProviderTest_TearDown()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        #endregion

        #region Private Methods

        private void ClearTestData()
        {
            var collection = _database.GetCollection(UITestCollection);
            collection.RemoveAll();

        }

        #endregion

        #region GetActive Tests

        [Test]
        public void GetActive_WorkflowsExist_ReturnsWorkflows()
        {
            var coll = _database.GetCollection(UITestCollection);
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();
            
            IEnumerable<UIWorkflowContainer> workflows = provider.GetActive(10);
            Assert.AreEqual(2, workflows.Count());
        }

        [Test]
        public void GetActive_WorkflowExist_CountObeyed()
        {
            var coll = _database.GetCollection(UITestCollection);
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());
            coll.Save(typeof(WorkflowContainer), CreateWorkflow());

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            IEnumerable<UIWorkflowContainer> workflows = provider.GetActive(3);
            Assert.AreEqual(3, workflows.Count());
        }

        [Test]
        public void GetActive_WorkflowDoNotExist_EmptyCollectionReturned()
        {
            var coll = _database.GetCollection(UITestCollection);

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            IEnumerable<UIWorkflowContainer> workflows = provider.GetActive(3);
            Assert.AreEqual(0, workflows.Count());
        }

        #endregion

        #region Suspend Tests

        [Test]
        public void Suspend_ExistingWorkflow_IsSuspended()
        {
            var coll = _database.GetCollection(UITestCollection);
            WorkflowContainer workflow = CreateWorkflow();
            coll.Save(typeof(WorkflowContainer), workflow);

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            provider.SuspendWorkflow(workflow.Id);

            BsonDocument doc = coll.FindOneById(BsonValue.Create(workflow.Id));
            WorkflowContainer result = BsonSerializer.Deserialize<WorkflowContainer>(doc);
            Assert.IsTrue(result.Workflow.IsSuspended);
        }

        [Test]
        public void Suspend_InvalidWorkflow_Skips()
        {
            var coll = _database.GetCollection(UITestCollection);
            Guid id = Guid.NewGuid();

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            provider.SuspendWorkflow(id);

            BsonDocument doc = coll.FindOneById(BsonValue.Create(id));
            Assert.IsNull(doc);
        }

        #endregion

        #region Unsuspend Tests

        [Test]
        public void Unsuspend_ExistingWorkflow_IsResumed()
        {
            var coll = _database.GetCollection(UITestCollection);
            WorkflowContainer workflow = CreateWorkflow(isSuspended: true, retryCount: 5, resumeOn: DateTime.UtcNow.AddDays(1));
            coll.Save(typeof(WorkflowContainer), workflow);

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            provider.UnsuspendWorkflow(workflow.Id);

            BsonDocument doc = coll.FindOneById(BsonValue.Create(workflow.Id));
            WorkflowContainer result = BsonSerializer.Deserialize<WorkflowContainer>(doc);
            Assert.IsFalse(result.Workflow.IsSuspended);
            Assert.AreEqual(0, result.Workflow.RetryCount);
            Assert.GreaterOrEqual(DateTime.UtcNow, result.Workflow.ResumeOn);
        }

        [Test]
        public void Unsuspend_InvalidWorkflow_Skips()
        {
            var coll = _database.GetCollection(UITestCollection);
            Guid id = Guid.NewGuid();

            MongoDbWorkflowProvider provider = (MongoDbWorkflowProvider)GetProvider();

            provider.UnsuspendWorkflow(id);

            BsonDocument doc = coll.FindOneById(BsonValue.Create(id));
            Assert.IsNull(doc);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Gets the provider relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected IWorkflowProvider GetProvider()
        {
            return new MongoDbWorkflowProvider(_conn);
        }

        #endregion

        #region Private Methods

        private WorkflowContainer CreateWorkflow(BasicWorkflow.State state = BasicWorkflow.State.Start, bool isSuspended = false, DateTime? resumeOn = null, int retryCount = 0)
        {
            WorkflowContainer container = new WorkflowContainer();

            BasicWorkflow bw = new BasicWorkflow();
            bw.CurrentState = state.ToString();
            bw.CreatedOn = DateTime.UtcNow;
            bw.IsSuspended = isSuspended;
            bw.ResumeOn = resumeOn ?? DateTime.UtcNow;
            bw.RetryCount = retryCount;
            bw.RetryIntervals = new int[] { 5, 10, 15 };

            container.Workflow = bw;
            container.WorkflowType = bw.GetType().ToString();

            return container;
        }

        #endregion



    }
}
