using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Connections;
using Stateless.WorkflowEngine.WebConsole.Features.Dashboards;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Features.Dashboards
{
    [TestFixture]
    public class DashboardsControllerTests
    {
        private IUserStore _userStore = null!;
        private DashboardValidator _validator = null!;
        private IWorkflowStoreFactory _workflowStoreFactory = null!;
        private MemoryCache _memoryCache = null!;
        private DashboardsController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _validator = Substitute.For<DashboardValidator>(_userStore);
            _validator.Validate(Arg.Any<DashboardRequest>(), Arg.Any<Guid?>()).Returns(new List<string>());
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _controller = new DashboardsController(_userStore, _validator, _workflowStoreFactory, _memoryCache);
        }

        [TearDown]
        public void TearDown()
        {
            _memoryCache.Dispose();
        }

        // --- List ---

        [Test]
        public void List_ReturnsDefaultFirstThenByName_WithConnectionCounts()
        {
            var zebra = new DashboardModel { Name = "zebra" };
            var main = new DashboardModel { Name = "Main", IsDefault = true };
            var apple = new DashboardModel { Name = "Apple" };
            _userStore.Dashboards.Returns(new[] { zebra, main, apple });
            _userStore.Connections.Returns(new[] { CreateConnection(apple.Id), CreateConnection(apple.Id), CreateConnection(main.Id) });

            var result = (OkObjectResult)_controller.List();

            var dashboards = ((IEnumerable<DashboardResponse>)result.Value!).ToList();
            Assert.That(dashboards.Select(x => x.Name), Is.EqualTo(new[] { "Main", "Apple", "zebra" }));
            Assert.That(dashboards.Select(x => x.ConnectionCount), Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(dashboards[0].IsDefault, Is.True);
        }

        // --- Get ---

        [Test]
        public void Get_Returns404_WhenDashboardNotFound()
        {
            var result = _controller.Get(Guid.NewGuid());

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        // --- Add ---

        [Test]
        public void Add_Returns400_WhenInvalid()
        {
            _validator.Validate(Arg.Any<DashboardRequest>(), null).Returns(new List<string> { "Name cannot be empty" });

            var result = _controller.Add(new DashboardRequest());

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Name cannot be empty" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveDashboard(default!);
        }

        [Test]
        public void Add_SavesNonDefaultDashboard_WithTrimmedName()
        {
            var result = (OkObjectResult)_controller.Add(new DashboardRequest { Name = "  Production " });

            Assert.That(((DashboardResponse)result.Value!).Name, Is.EqualTo("Production"));
            _userStore.Received(1).SaveDashboard(Arg.Is<DashboardModel>(x => x.Name == "Production" && !x.IsDefault));
        }

        // --- Update ---

        [Test]
        public void Update_Returns404_WhenDashboardNotFound()
        {
            var result = _controller.Update(Guid.NewGuid(), new DashboardRequest { Name = "x" });

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Update_RenamesDefaultDashboard_KeepingItDefault()
        {
            var existing = new DashboardModel { Name = DashboardModel.DefaultName, IsDefault = true };
            _userStore.GetDashboard(existing.Id).Returns(existing);

            var result = _controller.Update(existing.Id, new DashboardRequest { Name = "Production" });

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _validator.Received(1).Validate(Arg.Any<DashboardRequest>(), existing.Id);
            _userStore.Received(1).SaveDashboard(Arg.Is<DashboardModel>(x => x.Id == existing.Id && x.Name == "Production" && x.IsDefault));
        }

        // --- Delete ---

        [Test]
        public void Delete_Returns404_WhenDashboardNotFound()
        {
            var result = _controller.Delete(Guid.NewGuid());

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
            _userStore.DidNotReceiveWithAnyArgs().DeleteDashboard(default);
        }

        [Test]
        public void Delete_Returns400_WhenDefaultDashboard()
        {
            var existing = new DashboardModel { Name = DashboardModel.DefaultName, IsDefault = true };
            _userStore.GetDashboard(existing.Id).Returns(existing);

            var result = _controller.Delete(existing.Id);

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "The default dashboard cannot be deleted" }));
            _userStore.DidNotReceiveWithAnyArgs().DeleteDashboard(default);
        }

        [Test]
        public void Delete_RemovesDashboard_AndEvictsItsConnections()
        {
            var existing = new DashboardModel { Name = "Other" };
            var onDashboard = CreateConnection(existing.Id);
            var elsewhere = CreateConnection(Guid.NewGuid());
            _userStore.GetDashboard(existing.Id).Returns(existing);
            _userStore.Connections.Returns(new[] { onDashboard, elsewhere });
            _memoryCache.Set(ConnectionsController.InfoCacheKey(onDashboard.Id), new WorkflowStoreInfo());

            var result = _controller.Delete(existing.Id);

            Assert.That(result, Is.TypeOf<NoContentResult>());
            _userStore.Received(1).DeleteDashboard(existing.Id);
            _workflowStoreFactory.Received(1).Evict(onDashboard.Id);
            _workflowStoreFactory.DidNotReceive().Evict(elsewhere.Id);
            Assert.That(_memoryCache.TryGetValue(ConnectionsController.InfoCacheKey(onDashboard.Id), out _), Is.False);
        }

        private static ConnectionModel CreateConnection(Guid dashboardId)
        {
            var connection = TestHelpers.CreateConnection();
            connection.DashboardId = dashboardId;
            return connection;
        }
    }

    [TestFixture]
    public class DashboardValidatorTests
    {
        private IUserStore _userStore = null!;
        private DashboardValidator _validator = null!;
        private DashboardModel _existing = null!;

        [SetUp]
        public void SetUp()
        {
            _existing = new DashboardModel { Name = "Production" };
            _userStore = Substitute.For<IUserStore>();
            _userStore.Dashboards.Returns(new[] { _existing });
            _validator = new DashboardValidator(_userStore);
        }

        [Test]
        public void Validate_ReturnsNoErrors_WhenValid()
        {
            Assert.That(_validator.Validate(new DashboardRequest { Name = "Staging" }, null), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Validate_ReturnsError_WhenNameEmpty(string? name)
        {
            Assert.That(_validator.Validate(new DashboardRequest { Name = name }, null), Is.EqualTo(new[] { "Name cannot be empty" }));
        }

        [Test]
        public void Validate_ReturnsError_WhenNameTooLong()
        {
            var request = new DashboardRequest { Name = new string('a', DashboardValidator.MaxNameLength + 1) };

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { "Name cannot be longer than 100 characters" }));
        }

        [Test]
        public void Validate_ReturnsError_WhenNameUsedByAnotherDashboard()
        {
            Assert.That(_validator.Validate(new DashboardRequest { Name = " production " }, null), Is.EqualTo(new[] { "A dashboard with the supplied name already exists" }));
        }

        [Test]
        public void Validate_AllowsSameName_WhenRenamingTheSameDashboard()
        {
            Assert.That(_validator.Validate(new DashboardRequest { Name = "PRODUCTION" }, _existing.Id), Is.Empty);
        }
    }
}
