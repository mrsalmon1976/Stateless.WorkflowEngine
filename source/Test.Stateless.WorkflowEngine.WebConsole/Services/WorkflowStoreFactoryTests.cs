using Microsoft.AspNetCore.DataProtection;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Services
{
    [TestFixture]
    public class WorkflowStoreFactoryTests
    {
        private ConnectionPasswordProtector _protector = null!;
        private WorkflowStoreFactory _factory = null!;

        [SetUp]
        public void SetUp()
        {
            _protector = new ConnectionPasswordProtector(new EphemeralDataProtectionProvider(), new LegacyEncryptionProvider());
            _factory = new WorkflowStoreFactory(_protector);
        }

        [Test]
        public void GetWorkflowStore_UsesConfiguredCollections()
        {
            var connection = TestHelpers.CreateConnection();
            connection.ActiveCollection = "Active";
            connection.CompletedCollection = "Done";
            connection.WorkflowDefinitionCollection = "Defs";

            var store = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);

            Assert.That(store.CollectionActive, Is.EqualTo("Active"));
            Assert.That(store.CollectionCompleted, Is.EqualTo("Done"));
            Assert.That(store.CollectionDefinitions, Is.EqualTo("Defs"));
        }

        [Test]
        public void GetWorkflowStore_UsesDefaultDefinitionCollection_WhenNotSet()
        {
            var connection = TestHelpers.CreateConnection();
            connection.WorkflowDefinitionCollection = " ";

            var store = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);

            Assert.That(store.CollectionDefinitions, Is.EqualTo(MongoDbWorkflowStore.DefaultCollectionDefinitions));
        }

        [Test]
        public void GetMongoUrl_IncludesCredentialsAndReplicaSet()
        {
            var connection = TestHelpers.CreateConnection("mongo.local");
            connection.Port = 27018;
            connection.User = "joe";
            connection.Password = _protector.Protect("secret");
            connection.ReplicaSet = "rs0";

            var url = _factory.GetMongoUrl(connection);

            Assert.That(url.Server.Host, Is.EqualTo("mongo.local"));
            Assert.That(url.Server.Port, Is.EqualTo(27018));
            Assert.That(url.Username, Is.EqualTo("joe"));
            Assert.That(url.Password, Is.EqualTo("secret"));
            Assert.That(url.ReplicaSetName, Is.EqualTo("rs0"));
        }
    }
}
