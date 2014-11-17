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
        private MongoDatabase _database = null;

        #region SetUp and TearDown

        [TestFixtureSetUp]
        public void MongoDbWorkflowStoreTest_FixtureSetUp()
        {
            var connectionString = "mongodb://localhost";
            var client = new MongoClient(connectionString);
            var server = client.GetServer();
            _database = server.GetDatabase("StatelessWorkflowTest");
        }

        [TestFixtureTearDown]
        public void MongoDbWorkflowStoreTest_FixtureTearDown()
        {
        }

        [SetUp]
        public void MongoDbWorkflowStoreTest_SetUp()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        [TearDown]
        public void MongoDbWorkflowStoreTest_TearDown()
        {
            // make sure there is no data in the database for the next test
            ClearTestData();
        }

        #endregion

        #region Private Methods

        private void ClearTestData()
        {
            var collection = _database.GetCollection<WorkflowContainer>(MongoDbWorkflowStore.DefaultCollectionActive);
            collection.RemoveAll();

            collection = _database.GetCollection<WorkflowContainer>(MongoDbWorkflowStore.DefaultCollectionCompleted);
            collection.RemoveAll();
        }

        #endregion

        #region GetCollection Tests

        [Test]
        public void GetCollection_ActiveCollectionNotSet_ReturnsDefaultValue()
        {
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            var coll = store.GetCollection();
            Assert.AreEqual(MongoDbWorkflowStore.DefaultCollectionActive, coll.Name);
        }

        [Test]
        public void GetCollection_ActiveCollectionSet_ReturnsCorrectValue()
        {
            const string testCollection = "WorkflowTest";
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            store.CollectionActive = testCollection;
            var coll = store.GetCollection();
            Assert.AreEqual(testCollection, coll.Name);
            _database.DropCollection(testCollection);
        }

        #endregion

        #region GetCompletedCollection Tests

        [Test]
        public void GetCompletedCollection_CompletedCollectionNotSet_ReturnsDefaultValue()
        {
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            var coll = store.GetCompletedCollection();
            Assert.AreEqual(MongoDbWorkflowStore.DefaultCollectionCompleted, coll.Name);
        }

        [Test]
        public void GetCompletedCollection_CompletedCollectionSet_ReturnsCorrectValue()
        {
            const string testCollection = "WorkflowCompletedTest";
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)GetStore();
            store.CollectionCompleted = testCollection;
            var coll = store.GetCompletedCollection();
            Assert.AreEqual(testCollection, coll.Name);
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

        #endregion

    }
}
