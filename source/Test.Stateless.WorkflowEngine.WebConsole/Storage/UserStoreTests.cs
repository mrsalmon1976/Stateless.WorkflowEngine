using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;
using System.IO.Abstractions.TestingHelpers;

namespace Test.Stateless.WorkflowEngine.WebConsole.Storage
{
    [TestFixture]
    public class UserStoreTests
    {
        private const string FilePath = @"C:\console\Data\users.json";

        private MockFileSystem _fileSystem = null!;
        private IPasswordProvider _passwordProvider = null!;
        private LegacyEncryptionProvider _legacyEncryptionProvider = null!;
        private ConnectionPasswordProtector _connectionPasswordProtector = null!;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _passwordProvider = Substitute.For<IPasswordProvider>();
            _legacyEncryptionProvider = new LegacyEncryptionProvider();
            _connectionPasswordProtector = new ConnectionPasswordProtector(new EphemeralDataProtectionProvider(), _legacyEncryptionProvider);
        }

        [Test]
        public void Load_CreatesDefaultAdmin_WhenFileDoesNotExist()
        {
            _passwordProvider.HashPassword("admin").Returns("hashed");
            var store = CreateStore();

            store.Load();

            Assert.That(store.Users, Has.Count.EqualTo(1));
            Assert.That(store.Users[0].UserName, Is.EqualTo("admin"));
            Assert.That(store.Users[0].Password, Is.EqualTo("hashed"));
            Assert.That(store.Users[0].Role, Is.EqualTo(Roles.Admin));
            Assert.That(store.Connections, Is.Empty);
            Assert.That(_fileSystem.File.Exists(FilePath), Is.False);
        }

        [Test]
        public void Load_ReadsLegacyFileFormat()
        {
            var userId = Guid.NewGuid();
            var connectionId = Guid.NewGuid();
            _fileSystem.AddFile(FilePath, new MockFileData($$"""
                {
                  "Users": [ { "Id": "{{userId}}", "UserName": "joe", "Password": "hash", "Role": "User", "Claims": [ "UserList" ] } ],
                  "Connections": [ { "Id": "{{connectionId}}", "WorkflowStoreType": 0, "Host": "localhost", "Database": "db", "User": null, "Password": null, "Key": null, "Port": 27017, "ReplicaSet": null, "ActiveCollection": "Workflows", "CompletedCollection": "CompletedWorkflows", "WorkflowDefinitionCollection": null } ]
                }
                """));
            var store = CreateStore();

            store.Load();

            Assert.That(store.GetUser(userId)!.UserName, Is.EqualTo("joe"));
            Assert.That(store.GetUser("JOE")!.Claims, Is.EquivalentTo(new[] { "UserList" }));
            Assert.That(store.GetConnection(connectionId)!.Host, Is.EqualTo("localhost"));
        }

        [Test]
        public void Load_MigratesLegacyConnectionPasswordsAndSaves()
        {
            byte[] key = _legacyEncryptionProvider.NewKey();
            var connection = TestHelpers.CreateConnection();
            connection.Key = Convert.ToBase64String(key);
            connection.Password = _legacyEncryptionProvider.SimpleEncrypt("secret", key);
            _fileSystem.AddFile(FilePath, new MockFileData(Newtonsoft.Json.JsonConvert.SerializeObject(new { Users = new UserModel[0], Connections = new[] { connection } })));
            var store = CreateStore();

            store.Load();

            var loaded = store.GetConnection(connection.Id)!;
            Assert.That(loaded.Key, Is.Null);
            Assert.That(_connectionPasswordProtector.Unprotect(loaded), Is.EqualTo("secret"));
            var saved = JObject.Parse(_fileSystem.File.ReadAllText(FilePath));
            Assert.That(saved["Connections"]![0]!["Key"]!.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void SaveUser_AddsNewUserAndWritesFile()
        {
            var store = CreateStore();
            store.Load();
            var user = TestHelpers.CreateUser("joe");

            store.SaveUser(user);

            Assert.That(store.Users, Has.Count.EqualTo(2));
            var saved = JObject.Parse(_fileSystem.File.ReadAllText(FilePath));
            Assert.That(saved["Users"]!.Count(), Is.EqualTo(2));
            Assert.That(saved["Connections"], Is.Not.Null);
        }

        [Test]
        public void SaveUser_ReplacesExistingUser()
        {
            var store = CreateStore();
            store.Load();
            var user = TestHelpers.CreateUser("joe");
            store.SaveUser(user);

            store.SaveUser(new UserModel { Id = user.Id, UserName = "joseph", Role = Roles.User });

            Assert.That(store.Users, Has.Count.EqualTo(2));
            Assert.That(store.GetUser(user.Id)!.UserName, Is.EqualTo("joseph"));
        }

        [Test]
        public void SaveAndDeleteConnection_UpdatesStoreAndFile()
        {
            var store = CreateStore();
            store.Load();
            var connection = TestHelpers.CreateConnection();

            store.SaveConnection(connection);
            Assert.That(store.GetConnection(connection.Id), Is.Not.Null);

            store.DeleteConnection(connection.Id);
            Assert.That(store.GetConnection(connection.Id), Is.Null);
            var saved = JObject.Parse(_fileSystem.File.ReadAllText(FilePath));
            Assert.That(saved["Connections"]!.Count(), Is.EqualTo(0));
        }

        [Test]
        public void DeleteUser_RemovesUser()
        {
            var store = CreateStore();
            store.Load();
            var user = TestHelpers.CreateUser("joe");
            store.SaveUser(user);

            store.DeleteUser(user.Id);

            Assert.That(store.GetUser(user.Id), Is.Null);
        }

        [Test]
        public void SavedFile_CanBeReloaded()
        {
            var store = CreateStore();
            store.Load();
            var connection = TestHelpers.CreateConnection();
            connection.Password = _connectionPasswordProtector.Protect("pwd");
            store.SaveConnection(connection);

            var reloaded = CreateStore();
            reloaded.Load();

            Assert.That(reloaded.Users.Select(x => x.UserName), Is.EquivalentTo(new[] { "admin" }));
            Assert.That(_connectionPasswordProtector.Unprotect(reloaded.GetConnection(connection.Id)!), Is.EqualTo("pwd"));
        }

        private UserStore CreateStore()
        {
            return new UserStore(FilePath, _fileSystem, _passwordProvider, _connectionPasswordProtector, NullLogger<UserStore>.Instance);
        }
    }
}
