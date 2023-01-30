using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using System.IO;
using Stateless.WorkflowEngine.Models;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
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
    public class MongoDbSchemaServiceTest
    {
        private IMongoDatabase _database = null;

        #region SetUp and TearDown

        [OneTimeSetUp]
        public void MongoDbSchemaServiceTest_OneTimeSetUp()
        {
            var connectionString = "mongodb://localhost";
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

            Assert.AreEqual(1, collectionCount);
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

            Assert.AreEqual(1, collectionCount);
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
            
            Assert.AreEqual(2, indexes.Count);
            bool result = IndexExists(indexes, IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            Assert.IsTrue(result);
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

            Assert.AreEqual(2, indexes.Count);
            bool result = IndexExists(indexes, IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            Assert.IsTrue(result);
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

            Assert.AreEqual(2, indexes.Count);
            bool result = IndexExists(indexes, IndexNames.CompletedWorkflow_CreatedOn);
            Assert.IsTrue(result);
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

            Assert.AreEqual(2, indexes.Count);
            bool result = IndexExists(indexes, IndexNames.CompletedWorkflow_CreatedOn);
            Assert.IsTrue(result);
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
