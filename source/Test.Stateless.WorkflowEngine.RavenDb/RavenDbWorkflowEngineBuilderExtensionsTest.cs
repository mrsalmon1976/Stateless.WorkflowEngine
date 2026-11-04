using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Raven.Client.Documents;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.Stores;
using System;

namespace Test.Stateless.WorkflowEngine.RavenDb
{
    /// <summary>
    /// Tests the RavenDb store registration - the store is resolved but not used, so no RavenDb server is needed.
    /// </summary>
    [TestFixture]
    public class RavenDbWorkflowEngineBuilderExtensionsTest
    {
        [Test]
        public void WithRavenDbStore_Urls_StoreUsesInitialisedDocumentStore()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithRavenDbStore(new[] { "http://localhost:8080" }, "DiTest");

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            RavenDbWorkflowStore store = (RavenDbWorkflowStore)serviceProvider.GetRequiredService<IWorkflowStore>();

            Assert.That(store.DocumentStore.Database, Is.EqualTo("DiTest"));
            Assert.That(store.DocumentStore.Urls, Is.EqualTo(new[] { "http://localhost:8080" }));

            bool disposed = false;
            store.DocumentStore.AfterDispose += (sender, e) => disposed = true;
            serviceProvider.Dispose();
            Assert.That(disposed, Is.True);
        }

        [Test]
        public void WithRavenDbStore_DocumentStoreFactory_StoreUsesDocumentStoreFromServiceProvider()
        {
            IDocumentStore documentStore = Substitute.For<IDocumentStore>();
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton(documentStore);
            services.AddStatelessWorkflowEngine().WithRavenDbStore(sp => sp.GetRequiredService<IDocumentStore>());

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            RavenDbWorkflowStore store = (RavenDbWorkflowStore)serviceProvider.GetRequiredService<IWorkflowStore>();

            Assert.That(store.DocumentStore, Is.SameAs(documentStore));
        }

        [TestCase(null, "DiTest")]
        [TestCase("http://localhost:8080", " ")]
        public void WithRavenDbStore_MissingConnectionDetails_ThrowsArgumentException(string url, string database)
        {
            ServiceCollection services = new ServiceCollection();
            IWorkflowEngineBuilder builder = services.AddStatelessWorkflowEngine();

            Assert.Throws<ArgumentException>(() => builder.WithRavenDbStore(url == null ? new string[0] : new[] { url }, database));
        }
    }
}
