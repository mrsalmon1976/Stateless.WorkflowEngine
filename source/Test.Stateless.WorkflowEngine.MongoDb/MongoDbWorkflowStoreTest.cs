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
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using System.Threading.Tasks;

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
            var connectionString = TestSettings.MongoDbConnectionString;
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

        [TestCase(true)]
        [TestCase(false)]
        public void Initialise_AutoCreateIndexTrue_LockedByIndexIsCreated(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            workflowStore.Initialise(autoCreateTables, true, false);

            schemaService.Received(1).EnsureLockedByIndexExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            schemaService.Received(1).EnsureCollectionExists(Arg.Any<IMongoDatabase>(), workflowStore.CollectionNodes);
        }

        [Test]
        public async Task InitialiseAsync_AutoCreateTablesFalseAndAutoCreateIndexesFalse_NothingIsCreated()
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            await workflowStore.InitialiseAsync(false, false, false);

            await schemaService.DidNotReceive().EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
            await schemaService.DidNotReceive().EnsureActiveIndexExistsAsync(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task InitialiseAsync_AutoCreateIndexFalse_CollectionsCreatedOnlyIfAutoCreateTables(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            await workflowStore.InitialiseAsync(autoCreateTables, false, true);

            int expected = autoCreateTables ? 1 : 0;
            await schemaService.Received(expected).EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            await schemaService.Received(expected).EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionDefinitions);
            await schemaService.DidNotReceive().EnsureActiveIndexExistsAsync(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
            schemaService.DidNotReceive().EnsureCollectionExists(Arg.Any<IMongoDatabase>(), Arg.Any<string>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task InitialiseAsync_AutoCreateIndexTrue_CollectionsAndIndexesAreCreated(bool autoCreateTables)
        {
            IMongoDbSchemaService schemaService = Substitute.For<IMongoDbSchemaService>();
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            workflowStore.SchemaService = schemaService;

            await workflowStore.InitialiseAsync(autoCreateTables, true, false);

            await schemaService.Received(1).EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            await schemaService.Received(1).EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionCompleted);
            await schemaService.Received(1).EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionNodes);
            await schemaService.DidNotReceive().EnsureCollectionExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionDefinitions);
            await schemaService.Received(1).EnsureActiveIndexExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            await schemaService.Received(1).EnsureSuspendedIndexExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            await schemaService.Received(1).EnsureLockedByIndexExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionActive);
            await schemaService.Received(1).EnsureCompletedIndexExistsAsync(Arg.Any<IMongoDatabase>(), workflowStore.CollectionCompleted);
        }

        [Test]
        public async Task InitialiseAsync_RealSchemaService_CreatesIndexesOnce()
        {
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();

            // a second call finds the existing collections and indexes, so must not fail
            await workflowStore.InitialiseAsync(true, true, true);
            await workflowStore.InitialiseAsync(true, true, true);

            var indexNames = (await workflowStore.GetCollection().Indexes.ListAsync()).ToList().Select(x => x["name"].AsString).ToList();
            Assert.That(indexNames, Does.Contain(IndexNames.Workflow_Priority_RetryCount_CreatedOn));
            Assert.That(indexNames, Does.Contain(IndexNames.Workflow_IsSuspended));
            Assert.That(indexNames, Does.Contain(IndexNames.Workflow_LockedBy));
            var completedIndexNames = (await workflowStore.GetCompletedCollection().Indexes.ListAsync()).ToList().Select(x => x["name"].AsString).ToList();
            Assert.That(completedIndexNames, Does.Contain(IndexNames.CompletedWorkflow_CreatedOn));
        }

        [Test]
        public void Save_UnlockedWorkflow_LockFieldsNotStored()
        {
            // the partial LockedBy index relies on unlocked workflows not storing the lock fields at all
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflowStore.Save(workflow);

            BsonDocument unlocked = GetRawWorkflow(workflowStore, workflow.Id);
            Assert.That(unlocked.Contains("LockedBy"), Is.False);
            Assert.That(unlocked.Contains("LockedOn"), Is.False);

            workflowStore.ClaimNext("node1");
            BsonDocument locked = GetRawWorkflow(workflowStore, workflow.Id);
            Assert.That(locked["LockedBy"].AsString, Is.EqualTo("node1"));
            Assert.That(locked["LockedOn"].IsValidDateTime, Is.True);

            workflowStore.ReleaseLocks("node1");
            BsonDocument released = GetRawWorkflow(workflowStore, workflow.Id);
            Assert.That(released.Contains("LockedBy"), Is.False);
            Assert.That(released.Contains("LockedOn"), Is.False);
        }

        [Test]
        public void ClaimNext_LockedByStoredAsNull_WorkflowClaimed()
        {
            MongoDbWorkflowStore workflowStore = (MongoDbWorkflowStore)GetStore();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflowStore.Save(workflow);
            _database.GetCollection<BsonDocument>(workflowStore.CollectionActive).UpdateOne(
                new BsonDocument(),
                new BsonDocument("$set", new BsonDocument("Workflow.LockedBy", BsonNull.Value)));

            Assert.That(workflowStore.ClaimNext("node1")?.Id, Is.EqualTo(workflow.Id));
        }


        #endregion

        #region Private Methods

        private BsonDocument GetRawWorkflow(MongoDbWorkflowStore workflowStore, Guid id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, MongoDbWorkflowStore.IdGuidRepresentation));
            return _database.GetCollection<BsonDocument>(workflowStore.CollectionActive).Find(filter).Single()["Workflow"].AsBsonDocument;
        }

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
