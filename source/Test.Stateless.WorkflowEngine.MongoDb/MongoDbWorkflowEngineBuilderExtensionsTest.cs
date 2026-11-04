using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NUnit.Framework;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using System;

namespace Test.Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Tests the MongoDb store registration - the store is resolved but not used, so no MongoDb server is needed.
    /// </summary>
    [TestFixture]
    public class MongoDbWorkflowEngineBuilderExtensionsTest
    {
        [Test]
        public void WithMongoDbStore_ConnectionString_StoreUsesDatabase()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMongoDbStore("mongodb://localhost:27017", "DiTest");

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)serviceProvider.GetRequiredService<IWorkflowStore>();

            Assert.That(store.MongoDatabase.DatabaseNamespace.DatabaseName, Is.EqualTo("DiTest"));
            Assert.That(store.CollectionActive, Is.EqualTo(MongoDbWorkflowStore.DefaultCollectionActive));
        }

        [Test]
        public void WithMongoDbStore_Configure_AppliedToStore()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMongoDbStore("mongodb://localhost:27017", "DiTest", store =>
            {
                store.CollectionActive = "Active";
                store.CollectionCompleted = "Completed";
            });

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)serviceProvider.GetRequiredService<IWorkflowStore>();

            Assert.That(store.CollectionActive, Is.EqualTo("Active"));
            Assert.That(store.CollectionCompleted, Is.EqualTo("Completed"));
        }

        [Test]
        public void WithMongoDbStore_DatabaseFactory_StoreUsesDatabaseFromServiceProvider()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));
            services.AddStatelessWorkflowEngine().WithMongoDbStore(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("Shared"));

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            MongoDbWorkflowStore store = (MongoDbWorkflowStore)serviceProvider.GetRequiredService<IWorkflowStore>();

            Assert.That(store.MongoDatabase.Client, Is.SameAs(serviceProvider.GetRequiredService<IMongoClient>()));
            Assert.That(store.MongoDatabase.DatabaseNamespace.DatabaseName, Is.EqualTo("Shared"));
        }

        [TestCase(null, "DiTest")]
        [TestCase("mongodb://localhost:27017", "")]
        public void WithMongoDbStore_MissingConnectionDetails_ThrowsArgumentException(string connectionString, string databaseName)
        {
            ServiceCollection services = new ServiceCollection();
            IWorkflowEngineBuilder builder = services.AddStatelessWorkflowEngine();

            Assert.Throws<ArgumentException>(() => builder.WithMongoDbStore(connectionString, databaseName));
        }
    }
}
