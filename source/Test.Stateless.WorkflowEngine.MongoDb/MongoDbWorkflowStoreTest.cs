using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using System.IO;
using MongoDB.Driver;
using Test.Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.MongoDb;
using NSubstitute;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Test fixture for MongoDbWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    [Category("RequiresMongoDb")]
    public class MongoDbWorkflowStoreTest : WorkflowStoreTestBase
    {
        private IMongoDatabase _database = null;

        #region SetUp and TearDown

        [OneTimeSetUp]
        public void MongoDbWorkflowStoreTest_OneTimeSetUp()
        {
            var connectionString = "mongodb://localhost";
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase("StatelessWorkflowTest");
        }

        [OneTimeTearDown]
        public void MongoDbWorkflowStoreTest_OneTimeTearDown()
        {
        }

        [SetUp]
        public void MongoDbWorkflowStoreTest_SetUp()
        {
            // make sure there is no data in the database for the next test
            DeleteTestCollections();
        }

        [TearDown]
        public void MongoDbWorkflowStoreTest_TearDown()
        {
            DeleteTestCollections();
        }

        #endregion

        #region MongoDb-specific Tests

        [Test]
        public void Initialise_AutoCreateTablesFalseAndAutoCreateIndexesFalse_CollectionsAreNotCreated()
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(false, false, false);

            schemaService.DidNotReceive().EnsureCollectionExists(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
        }

        [Test]
        public void Initialise_AutoCreateTablesFalseButAutoCreateIndexesTrue_CollectionsAreCreated()
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(false, true, false);

            schemaService.Received(1).EnsureCollectionExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            schemaService.Received(1).EnsureCollectionExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionCompleted);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Initialise_AutoCreateTablesTrue_CollectionsAreCreated(bool autoCreateIndexes)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(true, autoCreateIndexes, false);

            schemaService.Received(1).EnsureCollectionExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            schemaService.Received(1).EnsureCollectionExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionCompleted);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Initialise_AutoCreateIndexFalse_IndexesAreNotCreated(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(autoCreateTables, false, false);

            schemaService.DidNotReceive().EnsureActiveIndexExists(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
            schemaService.DidNotReceive().EnsureActiveIndexExists(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Initialise_AutoCreateIndexTrue_WorkflowIndexesAreCreated(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(autoCreateTables, true, false);

            schemaService.Received(1).EnsureActiveIndexExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Initialise_AutoCreateIndexTrue_CompletedWorkflowIndexesAreCreated(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(autoCreateTables, true, false);

            schemaService.Received(1).EnsureCompletedIndexExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionCompleted);
        }


        #endregion

        #region Private Methods

        private void DeleteTestCollections()
        {
            var collections = _database.ListCollectionNames().ToList();
            foreach (string c in collections)
            {
                _database.DropCollection(c);
            }
        }


        #endregion

        #region GetCollection Tests

        [Test]
        public void GetCollection_ActiveCollectionNotSet_ReturnsDefaultValue()
        {
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            var coll = store.GetCollection();
            Assert.That(coll.CollectionNamespace.CollectionName, Is.EqualTo(MongoDbWorkflowStore.DefaultCollectionActive));
        }

        [Test]
        public void GetCollection_ActiveCollectionSet_ReturnsCorrectValue()
        {
            const string testCollection = "WorkflowTest";
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            store.CollectionActive = testCollection;
            var coll = store.GetCollection();
            Assert.That(coll.CollectionNamespace.CollectionName, Is.EqualTo(testCollection));
            _database.DropCollection(testCollection);
        }

        #endregion

        #region GetCompletedCollection Tests

        [Test]
        public void GetCompletedCollection_CompletedCollectionNotSet_ReturnsDefaultValue()
        {
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            var coll = store.GetCompletedCollection();
            Assert.That(coll.CollectionNamespace.CollectionName, Is.EqualTo(MongoDbWorkflowStore.DefaultCollectionCompleted));
        }

        [Test]
        public void GetCompletedCollection_CompletedCollectionSet_ReturnsCorrectValue()
        {
            const string testCollection = "WorkflowCompletedTest";
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            store.CollectionCompleted = testCollection;
            var coll = store.GetCompletedCollection();
            Assert.That(coll.CollectionNamespace.CollectionName, Is.EqualTo(testCollection));
            _database.DropCollection(testCollection);
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Gets the store relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected override IWorkflowStore GetStore()
        {
            return new MongoDbWorkflowStore(_database);
        }

        protected override T DeserializeJsonWorkflow<T>(string json)
        {
            MongoWorkflow container = BsonSerializer.Deserialize<MongoWorkflow>(json);
            return (T)container.Workflow;
        }

        #endregion

    }
}
