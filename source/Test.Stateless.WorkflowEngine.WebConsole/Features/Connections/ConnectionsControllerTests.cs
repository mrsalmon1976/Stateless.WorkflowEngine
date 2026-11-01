using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Connections;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Features.Connections
{
    [TestFixture]
    public class ConnectionsControllerTests
    {
        private IUserStore _userStore = null!;
        private ConnectionValidator _validator = null!;
        private IConnectionPasswordProtector _passwordProtector = null!;
        private IWorkflowStoreFactory _workflowStoreFactory = null!;
        private IWorkflowInfoService _workflowInfoService = null!;
        private MemoryCache _memoryCache = null!;
        private ConnectionsController _controller = null!;
        private DashboardModel _defaultDashboard = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _defaultDashboard = new DashboardModel { Name = DashboardModel.DefaultName, IsDefault = true };
            _userStore.DefaultDashboard.Returns(_defaultDashboard);
            _validator = Substitute.For<ConnectionValidator>();
            _validator.Validate(Arg.Any<ConnectionRequest>()).Returns(new List<string>());
            _passwordProtector = Substitute.For<IConnectionPasswordProtector>();
            _passwordProtector.Protect(Arg.Any<string>()).Returns(x => "protected:" + x.Arg<string>());
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
            _workflowInfoService = Substitute.For<IWorkflowInfoService>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _controller = new ConnectionsController(_userStore, _validator, _passwordProtector, _workflowStoreFactory, _workflowInfoService, _memoryCache, NullLogger<ConnectionsController>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _memoryCache.Dispose();
        }

        // --- List ---

        [Test]
        public void List_ReturnsConnectionsOrderedByHostThenDatabase_WithoutPasswords()
        {
            var c1 = TestHelpers.CreateConnection("b", "x");
            var c2 = TestHelpers.CreateConnection("a", "z");
            var c3 = TestHelpers.CreateConnection("a", "y");
            c3.Password = "secret";
            _userStore.Connections.Returns(new[] { c1, c2, c3 });

            var result = (OkObjectResult)_controller.List();

            var connections = ((IEnumerable<ConnectionResponse>)result.Value!).ToList();
            Assert.That(connections.Select(x => x.Id), Is.EqualTo(new[] { c3.Id, c2.Id, c1.Id }));
            Assert.That(connections[0].HasPassword, Is.True);
            Assert.That(connections[1].HasPassword, Is.False);
        }

        [Test]
        public void List_FiltersByDashboard_WhenSupplied()
        {
            var dashboardId = Guid.NewGuid();
            var c1 = TestHelpers.CreateConnection("a");
            c1.DashboardId = dashboardId;
            var c2 = TestHelpers.CreateConnection("b");
            c2.DashboardId = Guid.NewGuid();
            _userStore.Connections.Returns(new[] { c1, c2 });

            var result = (OkObjectResult)_controller.List(dashboardId);

            var connections = ((IEnumerable<ConnectionResponse>)result.Value!).ToList();
            Assert.That(connections.Select(x => x.Id), Is.EqualTo(new[] { c1.Id }));
            Assert.That(connections[0].DashboardId, Is.EqualTo(dashboardId));
        }

        // --- Add ---

        [Test]
        public void Add_Returns400_WhenInvalid()
        {
            _validator.Validate(Arg.Any<ConnectionRequest>()).Returns(new List<string> { "Host cannot be empty" });

            var result = _controller.Add(new ConnectionRequest());

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Host cannot be empty" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveConnection(default!);
        }

        [Test]
        public void Add_EncryptsPassword_WhenSupplied()
        {
            var result = _controller.Add(CreateRequest(user: "joe", password: "pwd"));

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.Password == "protected:pwd" && x.Key == null && x.User == "joe"));
        }

        [Test]
        public void Add_DoesNotEncrypt_WhenNoPassword()
        {
            _controller.Add(CreateRequest());

            _passwordProtector.DidNotReceiveWithAnyArgs().Protect(default!);
            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.Password == null));
        }

        [Test]
        public void Add_MapsDefinitionCollection()
        {
            var request = CreateRequest();
            request.WorkflowDefinitionCollection = "Defs";

            _controller.Add(request);

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.WorkflowDefinitionCollection == "Defs"));
        }

        [Test]
        public void Add_RavenDb_SavesCertificateAndIgnoresMongoDbSettings()
        {
            var request = CreateRavenDbRequest(certificatePath: " C:\\certs\\client.pfx ", password: "pwd");
            request.User = "joe";
            request.ReplicaSet = "rs0";
            request.ActiveCollection = "Workflows";
            request.CompletedCollection = "CompletedWorkflows";
            request.WorkflowDefinitionCollection = "Defs";

            var result = _controller.Add(request);

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x =>
                x.WorkflowStoreType == WorkflowStoreType.RavenDb
                && x.CertificatePath == "C:\\certs\\client.pfx"
                && x.Password == "protected:pwd"
                && x.User == null
                && x.ReplicaSet == null
                && x.ActiveCollection == null
                && x.CompletedCollection == null
                && x.WorkflowDefinitionCollection == null));
        }

        [Test]
        public void Add_UsesDefaultDashboard_WhenNoneSupplied()
        {
            _controller.Add(CreateRequest());

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.DashboardId == _defaultDashboard.Id));
        }

        [Test]
        public void Add_UsesSuppliedDashboard()
        {
            var dashboard = new DashboardModel { Name = "Other" };
            _userStore.GetDashboard(dashboard.Id).Returns(dashboard);
            var request = CreateRequest();
            request.DashboardId = dashboard.Id;

            _controller.Add(request);

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.DashboardId == dashboard.Id));
        }

        [Test]
        public void Add_Returns400_WhenDashboardNotFound()
        {
            var request = CreateRequest();
            request.DashboardId = Guid.NewGuid();

            var result = _controller.Add(request);

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Dashboard not found" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveConnection(default!);
        }

        // --- Copy ---

        [Test]
        public void Copy_Returns404_WhenConnectionNotFound()
        {
            var result = _controller.Copy(Guid.NewGuid(), new ConnectionCopyRequest { DashboardId = Guid.NewGuid() });

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Copy_Returns400_WhenDashboardNotFound()
        {
            var existing = TestHelpers.CreateConnection();
            _userStore.GetConnection(existing.Id).Returns(existing);

            var result = _controller.Copy(existing.Id, new ConnectionCopyRequest { DashboardId = Guid.NewGuid() });

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Dashboard not found" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveConnection(default!);
        }

        [Test]
        public void Copy_Returns400_WhenConnectionAlreadyOnDashboard()
        {
            var dashboard = new DashboardModel { Name = "Other" };
            var existing = TestHelpers.CreateConnection();
            existing.DashboardId = _defaultDashboard.Id;
            // same store, different casing on the host and different credentials
            var duplicate = TestHelpers.CreateConnection("LOCALHOST");
            duplicate.DashboardId = dashboard.Id;
            duplicate.User = "someone-else";
            _userStore.GetConnection(existing.Id).Returns(existing);
            _userStore.GetDashboard(dashboard.Id).Returns(dashboard);
            _userStore.Connections.Returns(new[] { existing, duplicate });

            var result = _controller.Copy(existing.Id, new ConnectionCopyRequest { DashboardId = dashboard.Id });

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "The connection already exists on the 'Other' dashboard" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveConnection(default!);
        }

        [Test]
        public void Copy_SavesNewConnectionWithCredentials_OnTargetDashboard()
        {
            var dashboard = new DashboardModel { Name = "Other" };
            var existing = TestHelpers.CreateConnection();
            existing.DashboardId = _defaultDashboard.Id;
            existing.User = "joe";
            existing.Password = "existing-cipher";
            existing.ReplicaSet = "rs0";
            existing.WorkflowDefinitionCollection = "Defs";
            // a different database on the target dashboard is not a duplicate
            var other = TestHelpers.CreateConnection(database: "otherdb");
            other.DashboardId = dashboard.Id;
            _userStore.GetConnection(existing.Id).Returns(existing);
            _userStore.GetDashboard(dashboard.Id).Returns(dashboard);
            _userStore.Connections.Returns(new[] { existing, other });

            var result = _controller.Copy(existing.Id, new ConnectionCopyRequest { DashboardId = dashboard.Id });

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x =>
                x.Id != existing.Id
                && x.DashboardId == dashboard.Id
                && x.Host == existing.Host
                && x.Database == existing.Database
                && x.User == "joe"
                && x.Password == "existing-cipher"
                && x.ReplicaSet == "rs0"
                && x.WorkflowDefinitionCollection == "Defs"));
        }

        // --- Update ---

        [Test]
        public void Update_Returns404_WhenConnectionNotFound()
        {
            var result = _controller.Update(Guid.NewGuid(), CreateRequest());

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Update_KeepsExistingPassword_WhenNoneSupplied()
        {
            var existing = TestHelpers.CreateConnection();
            existing.User = "joe";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);

            var result = _controller.Update(existing.Id, CreateRequest(host: "newhost", user: "joe"));

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.Id == existing.Id && x.Host == "newhost" && x.Password == "existing-cipher"));
        }

        [Test]
        public void Update_KeepsExistingDashboard()
        {
            var existing = TestHelpers.CreateConnection();
            existing.DashboardId = Guid.NewGuid();
            _userStore.GetConnection(existing.Id).Returns(existing);
            var request = CreateRequest();
            request.DashboardId = Guid.NewGuid();

            _controller.Update(existing.Id, request);

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.DashboardId == existing.DashboardId));
        }

        [Test]
        public void Update_ClearsPassword_WhenUserCleared()
        {
            var existing = TestHelpers.CreateConnection();
            existing.User = "joe";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);

            _controller.Update(existing.Id, CreateRequest());

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.User == null && x.Password == null));
        }

        [Test]
        public void Update_RavenDb_KeepsExistingPassword_WhenNoneSupplied()
        {
            var existing = TestHelpers.CreateRavenDbConnection();
            existing.CertificatePath = "client.pfx";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);

            _controller.Update(existing.Id, CreateRavenDbRequest(certificatePath: "client.pfx"));

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.CertificatePath == "client.pfx" && x.Password == "existing-cipher"));
        }

        [Test]
        public void Update_RavenDb_ClearsPassword_WhenCertificateCleared()
        {
            var existing = TestHelpers.CreateRavenDbConnection();
            existing.CertificatePath = "client.pfx";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);

            _controller.Update(existing.Id, CreateRavenDbRequest());

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.CertificatePath == null && x.Password == null));
        }

        [Test]
        public void Update_ClearsPassword_WhenStoreTypeChanged()
        {
            // a MongoDb password is not a certificate password
            var existing = TestHelpers.CreateConnection();
            existing.User = "joe";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);

            _controller.Update(existing.Id, CreateRavenDbRequest(certificatePath: "client.pfx"));

            _userStore.Received(1).SaveConnection(Arg.Is<ConnectionModel>(x => x.WorkflowStoreType == WorkflowStoreType.RavenDb && x.Password == null));
        }

        [Test]
        public void Update_EvictsCachedClientAndInfo()
        {
            var existing = TestHelpers.CreateConnection();
            _userStore.GetConnection(existing.Id).Returns(existing);
            _memoryCache.Set(ConnectionsController.InfoCacheKey(existing.Id), new WorkflowStoreInfo());

            _controller.Update(existing.Id, CreateRequest());

            _workflowStoreFactory.Received(1).Evict(existing.Id);
            Assert.That(_memoryCache.TryGetValue(ConnectionsController.InfoCacheKey(existing.Id), out _), Is.False);
        }

        // --- Delete ---

        [Test]
        public void Delete_Returns404_WhenConnectionNotFound()
        {
            var result = _controller.Delete(Guid.NewGuid());

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
            _userStore.DidNotReceiveWithAnyArgs().DeleteConnection(default);
        }

        [Test]
        public void Delete_RemovesConnection()
        {
            var existing = TestHelpers.CreateConnection();
            _userStore.GetConnection(existing.Id).Returns(existing);

            var result = _controller.Delete(existing.Id);

            Assert.That(result, Is.TypeOf<NoContentResult>());
            _userStore.Received(1).DeleteConnection(existing.Id);
            _workflowStoreFactory.Received(1).Evict(existing.Id);
        }

        // --- Info ---

        [Test]
        public void Info_Returns404_WhenConnectionNotFound()
        {
            var result = _controller.Info(Guid.NewGuid());

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Info_ReturnsAndCachesInfo()
        {
            var connection = TestHelpers.CreateConnection();
            var info = new WorkflowStoreInfo { ActiveCount = 5 };
            _userStore.GetConnection(connection.Id).Returns(connection);
            _workflowInfoService.GetWorkflowStoreInfo(connection).Returns(info);

            var result = (OkObjectResult)_controller.Info(connection.Id);
            _controller.Info(connection.Id);

            Assert.That(result.Value, Is.SameAs(info));
            _workflowInfoService.Received(1).GetWorkflowStoreInfo(connection);
        }

        [Test]
        public void Info_ReturnsCachedInfo_WithoutLoadingConnection()
        {
            var id = Guid.NewGuid();
            var info = new WorkflowStoreInfo();
            _memoryCache.Set(ConnectionsController.InfoCacheKey(id), info);

            var result = (OkObjectResult)_controller.Info(id);

            Assert.That(result.Value, Is.SameAs(info));
            _userStore.DidNotReceiveWithAnyArgs().GetConnection(default);
        }

        // --- Test ---

        [Test]
        public void Test_Returns400_WhenInvalid()
        {
            _validator.Validate(Arg.Any<ConnectionRequest>()).Returns(new List<string> { "error" });

            var result = _controller.Test(CreateRequest());

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public void Test_ReturnsSuccess_WhenStoreResponds()
        {
            var store = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);

            var result = (OkObjectResult)_controller.Test(CreateRequest());

            Assert.That(((ConnectionTestResponse)result.Value!).Success, Is.True);
            store.Received(1).GetIncompleteCount();
            _workflowStoreFactory.Received(1).Evict(Arg.Any<Guid>());
        }

        [Test]
        public void Test_ReturnsFailure_WhenStoreThrows()
        {
            var store = Substitute.For<IWorkflowStore>();
            store.GetIncompleteCount().Returns(x => throw new Exception("Command is not authorized"));
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(store);

            var result = (OkObjectResult)_controller.Test(CreateRequest());

            var response = (ConnectionTestResponse)result.Value!;
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Connection failed: Authentication failure"));
        }

        [Test]
        public void Test_UsesStoredPassword_WhenTestingExistingConnection()
        {
            var existing = TestHelpers.CreateConnection();
            existing.User = "joe";
            existing.Password = "existing-cipher";
            _userStore.GetConnection(existing.Id).Returns(existing);
            _workflowStoreFactory.GetWorkflowStore(Arg.Any<ConnectionModel>()).Returns(Substitute.For<IWorkflowStore>());
            var request = CreateRequest(user: "joe");
            request.Id = existing.Id;

            _controller.Test(request);

            _workflowStoreFactory.Received(1).GetWorkflowStore(Arg.Is<ConnectionModel>(x => x.Password == "existing-cipher" && x.Id != existing.Id));
        }

        private static ConnectionRequest CreateRequest(string host = "localhost", string? user = null, string? password = null)
        {
            return new ConnectionRequest
            {
                WorkflowStoreType = WorkflowStoreType.MongoDb,
                Host = host,
                Port = 27017,
                Database = "db",
                User = user,
                Password = password,
                PasswordConfirm = password,
                ActiveCollection = "Workflows",
                CompletedCollection = "CompletedWorkflows"
            };
        }

        private static ConnectionRequest CreateRavenDbRequest(string? certificatePath = null, string? password = null)
        {
            return new ConnectionRequest
            {
                WorkflowStoreType = WorkflowStoreType.RavenDb,
                Host = "localhost",
                Port = 8080,
                Database = "db",
                CertificatePath = certificatePath,
                Password = password,
                PasswordConfirm = password
            };
        }
    }

    [TestFixture]
    public class ConnectionValidatorTests
    {
        [Test]
        public void Validate_ReturnsNoErrors_WhenValid()
        {
            var errors = new ConnectionValidator().Validate(CreateValidRequest());

            Assert.That(errors, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Validate_ReturnsErrors_WhenRequiredFieldsEmpty(string? value)
        {
            var request = CreateValidRequest();
            request.Host = value;
            request.Database = value;
            request.ActiveCollection = value;
            request.CompletedCollection = value;

            var errors = new ConnectionValidator().Validate(request);

            Assert.That(errors, Is.EquivalentTo(new[] { "Host cannot be empty", "Database cannot be empty", "Active collection cannot be empty", "Completed collection cannot be empty" }));
        }

        [Test]
        public void Validate_RavenDb_DoesNotRequireCollections()
        {
            var request = new ConnectionRequest { WorkflowStoreType = WorkflowStoreType.RavenDb, Host = "localhost", Port = 8080, Database = "db" };

            Assert.That(new ConnectionValidator().Validate(request), Is.Empty);
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-1)]
        public void Validate_ReturnsError_WhenPortInvalid(int? port)
        {
            var request = CreateValidRequest();
            request.Port = port;

            Assert.That(new ConnectionValidator().Validate(request), Is.EqualTo(new[] { "Port must have a numeric value" }));
        }

        [Test]
        public void Validate_ReturnsError_WhenPasswordsDoNotMatch()
        {
            var request = CreateValidRequest();
            request.Password = "a";
            request.PasswordConfirm = "b";

            Assert.That(new ConnectionValidator().Validate(request), Is.EqualTo(new[] { "Password and confirmation password do not match" }));
        }

        private static ConnectionRequest CreateValidRequest()
        {
            return new ConnectionRequest { Host = "localhost", Port = 27017, Database = "db", ActiveCollection = "a", CompletedCollection = "c" };
        }
    }
}
