using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using MongoDB.Bson;
using System.Collections;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Test fixture for MongoDbWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    [Category("RequiresMongoDb")]
    public class MongoDbSchemaServiceTest
    {
        private IMongoDatabase _database = null;

        #region SetUp and TearDown

        [OneTimeSetUp]
        public void MongoDbSchemaServiceTest_OneTimeSetUp()
        {
            var connectionString = TestSettings.MongoDbConnectionString;
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase("StatelessWorkflowTest");
        }

        [OneTimeTearDown]
        public void MongoDbSchemaServiceTest_OneTimeTearDown()
        {
        }

        [SetUp]
        public void MongoDbSchemaServiceTest_SetUp()
        {
            // make sure there is no data in the database for the next test
            DeleteTestCollections();
        }

        [TearDown]
        public void MongoDbSchemaServiceTest_TearDown()
        {
            DeleteTestCollections();
        }

        #endregion

        #region EnsureCollectionExists Tests

        [Test]
        public void EnsureCollectionExists_WhenCalledOnce_CollectionIsCreated()
        {
            string collectionName = Path.GetRandomFileName();
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, collectionName);

            var collections = _database.ListCollectionNames().ToList();
            int collectionCount = 0;

            foreach (string c in collections)
            {
                if (c == collectionName)
                {
                    collectionCount++;
                }
            }

            Assert.That(collectionCount, Is.EqualTo(1));
        }

        [Test]
        public void EnsureCollectionExists_WhenCalledMultipleTimes_CollectionIsCreated()
        {
            string collectionName = Path.GetRandomFileName();
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();

            mongoDbSchemaService.EnsureCollectionExists(_database, collectionName);
            mongoDbSchemaService.EnsureCollectionExists(_database, collectionName);
            mongoDbSchemaService.EnsureCollectionExists(_database, collectionName);

            var collections = _database.ListCollectionNames().ToList();
            int collectionCount = 0;

            foreach (string c in collections)
            {
                if (c == collectionName)
                {
                    collectionCount++;
                }
            }

            Assert.That(collectionCount, Is.EqualTo(1));
        }


        #endregion

        #region EnsureActiveIndexExists Tests

        [Test]
        public void EnsureActiveIndexExists_WhenCalledOnce_IndexIsCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureActiveIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionActive);
            
            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            Assert.That(result, Is.True);
        }

        [Test]
        public void EnsureActiveIndexExists_WhenCalledMultipleTimes_IndexIsCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            
            mongoDbSchemaService.EnsureActiveIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureActiveIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureActiveIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionActive);

            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            Assert.That(result, Is.True);
        }

        [Test]
        public void EnsureActiveIndexExists_WhenIndexAlreadyExistsWithDifferentName_IndexIsNotCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var collection = _database.GetCollection<MongoWorkflow>(MongoDbWorkflowStore.DefaultCollectionActive);

            var testIndexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.Priority)
                .Descending(wf => wf.Workflow.RetryCount)
                .Ascending(wf => wf.Workflow.CreatedOn);

            var testIndexOptions = new CreateIndexOptions();
            testIndexOptions.Name = "test";

            var testIndexModel = new CreateIndexModel<MongoWorkflow>(testIndexKeys, testIndexOptions);
            collection.Indexes.CreateOne(testIndexModel);

            // execute
            mongoDbSchemaService.EnsureActiveIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionActive);

            // assert
            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            Assert.That(result, Is.False);
        }


        #endregion

        #region EnsureCompletedIndexExists Tests

        [Test]
        public void EnsureCompletedIndexExists_WhenCalledOnce_IndexIsCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);
            mongoDbSchemaService.EnsureCompletedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionCompleted);

            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.CompletedWorkflow_CreatedOn);
            Assert.That(result, Is.True);
        }

        [Test]
        public void EnsureCompletedIndexExists_WhenCalledMultipleTimes_IndexIsCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);

            mongoDbSchemaService.EnsureCompletedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);
            mongoDbSchemaService.EnsureCompletedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);
            mongoDbSchemaService.EnsureCompletedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionCompleted);

            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.CompletedWorkflow_CreatedOn);
            Assert.That(result, Is.True);
        }

        [Test]
        public void EnsureCompletedIndexExists_WhenIndexAlreadyExistsWithDifferentName_IndexIsNotCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);

            var collection = _database.GetCollection<MongoWorkflow>(MongoDbWorkflowStore.DefaultCollectionCompleted);

            var testIndexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.CreatedOn);

            var testIndexOptions = new CreateIndexOptions();
            testIndexOptions.Name = "test_completed";

            var testIndexModel = new CreateIndexModel<MongoWorkflow>(testIndexKeys, testIndexOptions);
            collection.Indexes.CreateOne(testIndexModel);

            // execute
            mongoDbSchemaService.EnsureCompletedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionCompleted);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionCompleted);

            // assert
            Assert.That(indexes.Count, Is.EqualTo(2));
            bool result = IndexExists(indexes, IndexNames.CompletedWorkflow_CreatedOn);
            Assert.That(result, Is.False);
        }


        #endregion

        #region EnsureSuspendedIndexExists Tests

        [Test]
        public void EnsureSuspendedIndexExists_WhenCalledOnce_PartialIndexIsCreated()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureSuspendedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionActive);

            Assert.That(indexes.Count, Is.EqualTo(2));
            var index = indexes.Single(x => x.GetValue("name").AsString == IndexNames.Workflow_IsSuspended);
            Assert.That(index["partialFilterExpression"], Is.EqualTo(new BsonDocument("Workflow.IsSuspended", true)));
        }

        [Test]
        public void EnsureSuspendedIndexExists_WhenCalledMultipleTimes_IndexIsCreatedOnce()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            mongoDbSchemaService.EnsureSuspendedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureSuspendedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureSuspendedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            var indexes = GetIndexList(MongoDbWorkflowStore.DefaultCollectionActive);

            Assert.That(indexes.Count, Is.EqualTo(2));
            Assert.That(IndexExists(indexes, IndexNames.Workflow_IsSuspended), Is.True);
        }

        [Test]
        public void EnsureSuspendedIndexExists_SuspendedCountUsesIndex()
        {
            IMongoDbSchemaService mongoDbSchemaService = new MongoDbSchemaService();
            mongoDbSchemaService.EnsureCollectionExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);
            mongoDbSchemaService.EnsureSuspendedIndexExists(_database, MongoDbWorkflowStore.DefaultCollectionActive);

            // explain the aggregation the driver runs for GetSuspendedCount (CountDocuments) - it must use the
            // index rather than falling back to a collection scan
            var pipeline = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("Workflow.IsSuspended", true)),
                new BsonDocument("$group", new BsonDocument { { "_id", 1 }, { "n", new BsonDocument("$sum", 1) } })
            };
            var explain = _database.RunCommand<BsonDocument>(new BsonDocument
            {
                { "explain", new BsonDocument
                    {
                        { "aggregate", MongoDbWorkflowStore.DefaultCollectionActive },
                        { "pipeline", pipeline },
                        { "cursor", new BsonDocument() }
                    }
                },
                { "verbosity", "queryPlanner" }
            });

            string plan = explain.ToJson();
            Assert.That(plan, Does.Contain(IndexNames.Workflow_IsSuspended));
            Assert.That(plan, Does.Not.Contain("COLLSCAN"));
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

        private List<BsonDocument> GetIndexList(string collectionName)
        {
            var indexManager = _database.GetCollection<MongoWorkflow>(collectionName).Indexes;
            return indexManager.List().ToList();
        }

        private bool IndexExists(IEnumerable<BsonDocument> indexDocuments, string indexName)
        {
            foreach (BsonDocument index in indexDocuments)
            {
                string currentIndexName = index.GetValue("name").AsString;
                if (indexName == currentIndexName)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion








    }
}
