using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using StructureMap;
using System.IO;
using Stateless.WorkflowEngine.Models;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
using MongoDB.Driver;

namespace Test.Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Test fixture for MongoDbWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    public class MongoDbWorkflowStoreTest : WorkflowStoreTestBase
    {

        #region SetUp and TearDown

        [TestFixtureSetUp]
        public void MongoDbWorkflowStoreTest_FixtureSetUp()
        {
            ObjectFactory.Configure(x => x.ForSingletonOf<MongoServer>().Use(() =>
            {
                var connectionString = "mongodb://localhost";
                var client = new MongoClient(connectionString);
                return client.GetServer();
            }));
            ObjectFactory.Configure(x => x.For<MongoDatabase>().Use(ctx =>
            {
                var server = ObjectFactory.GetInstance<MongoServer>();
                return server.GetDatabase("StatelessWorkflowTest");
            }));

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
            var db = ObjectFactory.GetInstance<MongoDatabase>();
            var collection = db.GetCollection<WorkflowContainer>("Workflows");
            collection.RemoveAll();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Gets the store relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected override IWorkflowStore GetStore()
        {
            return new MongoDbWorkflowStore();
        }

        #endregion

    }
}
