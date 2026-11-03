using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Workflows;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Features.Workflows
{
    [TestFixture]
    public class WorkflowsControllerTests
    {
        private IUserStore _userStore = null!;
        private IWorkflowStoreFactory _workflowStoreFactory = null!;
        private IWorkflowInfoService _workflowInfoService = null!;
        private IWorkflowStore _workflowStore = null!;
        private ConnectionModel _connection = null!;
        private WorkflowsController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
            _workflowInfoService = Substitute.For<IWorkflowInfoService>();
            _workflowStore = Substitute.For<IWorkflowStore>();
            _connection = TestHelpers.CreateConnection();
            _userStore.GetConnection(_connection.Id).Returns(_connection);
            _workflowStoreFactory.GetWorkflowStore(_connection).Returns(_workflowStore);
            _controller = new WorkflowsController(_userStore, _workflowStoreFactory, _workflowInfoService);
        }

        // --- List ---

        [Test]
        public async Task List_Returns404_WhenConnectionNotFound()
        {
            Assert.That(await _controller.List(Guid.NewGuid()), Is.TypeOf<NotFoundObjectResult>());
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(WorkflowsController.MaxCount + 1)]
        public async Task List_Returns400_WhenCountInvalid(int count)
        {
            Assert.That(await _controller.List(_connection.Id, count), Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task List_ReturnsIncompleteWorkflows()
        {
            var workflows = new[] { new UIWorkflow(), new UIWorkflow() };
            _workflowInfoService.GetIncompleteWorkflowsAsync(_connection, 25).Returns(Task.FromResult<IEnumerable<UIWorkflow>>(workflows));

            var result = (OkObjectResult)await _controller.List(_connection.Id, 25);

            Assert.That(result.Value, Is.SameAs(workflows));
        }

        // --- Get ---

        [Test]
        public async Task Get_Returns404_WhenConnectionNotFound()
        {
            Assert.That(await _controller.Get(Guid.NewGuid(), Guid.NewGuid()), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Get_Returns404_WhenWorkflowNotFound()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJsonAsync(workflowId).Returns(Task.FromResult<string?>(null));

            Assert.That(await _controller.Get(_connection.Id, workflowId), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Get_ReturnsWorkflowJsonAndSuspendedFlag()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJsonAsync(workflowId).Returns(Task.FromResult<string?>("{ json }"));
            var workflow = new UIWorkflow { IsSuspended = true };
            _workflowInfoService.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, "{ json }").Returns(workflow);

            var result = (OkObjectResult)await _controller.Get(_connection.Id, workflowId);

            var response = (WorkflowResponse)result.Value!;
            Assert.That(response.WorkflowJson, Is.EqualTo("{ json }"));
            Assert.That(response.IsSuspended, Is.True);
            Assert.That(response.Workflow, Is.SameAs(workflow));
        }

        [Test]
        public async Task Get_IncludesDefinitionGraph_WhenWorkflowHasQualifiedName()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJsonAsync(workflowId).Returns(Task.FromResult<string?>("{ json }"));
            _workflowInfoService.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, "{ json }").Returns(new UIWorkflow { QualifiedName = "Test.Workflow" });
            _workflowStore.GetDefinitionByQualifiedNameAsync("Test.Workflow").Returns(Task.FromResult<WorkflowDefinition?>(new WorkflowDefinition { Graph = "digraph {}" }));

            var result = (OkObjectResult)await _controller.Get(_connection.Id, workflowId);

            Assert.That(((WorkflowResponse)result.Value!).Workflow!.WorkflowGraph, Is.EqualTo("digraph {}"));
        }

        // --- Suspend / Unsuspend / Delete ---

        [Test]
        public async Task Suspend_Returns404_WhenConnectionNotFound()
        {
            var result = await _controller.Suspend(Guid.NewGuid(), new WorkflowIdsRequest { WorkflowIds = new[] { Guid.NewGuid() } });

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Suspend_Returns400_WhenNoWorkflowIds()
        {
            Assert.That(await _controller.Suspend(_connection.Id, new WorkflowIdsRequest()), Is.TypeOf<BadRequestObjectResult>());
        }

        [TestCase(1)]
        [TestCase(5)]
        public async Task Suspend_SuspendsEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = await _controller.Suspend(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) await _workflowStore.Received(1).SuspendWorkflowAsync(id);
        }

        [TestCase(1)]
        [TestCase(5)]
        public async Task Unsuspend_UnsuspendsEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = await _controller.Unsuspend(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) await _workflowStore.Received(1).UnsuspendWorkflowAsync(id);
        }

        [TestCase(1)]
        [TestCase(5)]
        public async Task Delete_DeletesEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = await _controller.Delete(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) await _workflowStore.Received(1).DeleteAsync(id);
        }

        // --- Definition ---

        [Test]
        public async Task Definition_Returns400_WhenNoQualifiedName()
        {
            Assert.That(await _controller.Definition(_connection.Id, " "), Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Definition_Returns404_WhenConnectionNotFound()
        {
            Assert.That(await _controller.Definition(Guid.NewGuid(), "A"), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Definition_Returns404_WhenDefinitionNotFound()
        {
            _workflowStore.GetDefinitionByQualifiedNameAsync("A").Returns(Task.FromResult<WorkflowDefinition?>(null));

            Assert.That(await _controller.Definition(_connection.Id, "A"), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task Definition_ReturnsDefinition()
        {
            var definition = new WorkflowDefinition { QualifiedName = "A", Graph = "digraph {}" };
            _workflowStore.GetDefinitionByQualifiedNameAsync("A").Returns(Task.FromResult<WorkflowDefinition?>(definition));

            var result = (OkObjectResult)await _controller.Definition(_connection.Id, "A");

            Assert.That(result.Value, Is.SameAs(definition));
        }
    }
}
