using Microsoft.AspNetCore.DataProtection;
using Raven.Client.Documents;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void GetWorkflowStore_ReusesClient_ForSameConnection()
        {
            var connection = TestHelpers.CreateConnection();

            var store1 = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);
            var store2 = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);

            Assert.That(store2.MongoDatabase.Client, Is.SameAs(store1.MongoDatabase.Client));
        }

        [Test]
        public void Evict_CreatesNewClient_OnNextGetWorkflowStore()
        {
            var connection = TestHelpers.CreateConnection();
            var store1 = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);

            _factory.Evict(connection.Id);
            var store2 = (MongoDbWorkflowStore)_factory.GetWorkflowStore(connection);

            Assert.That(store2.MongoDatabase.Client, Is.Not.SameAs(store1.MongoDatabase.Client));
        }

        [Test]
        public void Evict_UnknownConnection_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _factory.Evict(Guid.NewGuid()));
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

        [Test]
        public void GetWorkflowStore_RavenDb_ReusesDocumentStore_UntilEvicted()
        {
            var connection = TestHelpers.CreateRavenDbConnection(database: "ravendb");

            var store1 = (RavenDbWorkflowStore)_factory.GetWorkflowStore(connection);
            var documentStore1 = store1.DocumentStore;
            var documentStore2 = ((RavenDbWorkflowStore)_factory.GetWorkflowStore(connection)).DocumentStore;
            _factory.Evict(connection.Id);
            var documentStore3 = ((RavenDbWorkflowStore)_factory.GetWorkflowStore(connection)).DocumentStore;

            Assert.That(documentStore2, Is.SameAs(documentStore1));
            Assert.That(documentStore3, Is.Not.SameAs(documentStore1));
            Assert.That(documentStore1.Database, Is.EqualTo("ravendb"));
            Assert.That(documentStore1.Urls, Is.EqualTo(new[] { "http://localhost:8080" }));
        }

        [Test]
        public void GetRavenDbUrl_UsesHttp_WhenNoCertificate()
        {
            var connection = TestHelpers.CreateRavenDbConnection("raven.local");
            connection.Port = 8081;

            Assert.That(_factory.GetRavenDbUrl(connection), Is.EqualTo("http://raven.local:8081"));
        }

        [Test]
        public void GetRavenDbUrl_UsesHttps_WhenCertificateConfigured()
        {
            var connection = TestHelpers.CreateRavenDbConnection("raven.local");
            connection.Port = 443;
            connection.CertificatePath = "client.pfx";

            Assert.That(_factory.GetRavenDbUrl(connection), Is.EqualTo("https://raven.local"));
        }

        [Test]
        public void GetWorkflowStore_RavenDb_LoadsClientCertificate()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "secret"));
            }
            try
            {
                var connection = TestHelpers.CreateRavenDbConnection();
                connection.CertificatePath = path;
                connection.Password = _protector.Protect("secret");

                var documentStore = ((RavenDbWorkflowStore)_factory.GetWorkflowStore(connection)).DocumentStore;

                Assert.That(documentStore.Certificate.Subject, Is.EqualTo("CN=Test"));
                Assert.That(documentStore.Urls, Is.EqualTo(new[] { "https://localhost:8080" }));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
