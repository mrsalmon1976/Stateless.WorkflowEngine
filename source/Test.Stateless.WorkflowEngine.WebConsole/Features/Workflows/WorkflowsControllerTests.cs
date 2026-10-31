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
        public void List_Returns404_WhenConnectionNotFound()
        {
            Assert.That(_controller.List(Guid.NewGuid()), Is.TypeOf<NotFoundObjectResult>());
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(WorkflowsController.MaxCount + 1)]
        public void List_Returns400_WhenCountInvalid(int count)
        {
            Assert.That(_controller.List(_connection.Id, count), Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public void List_ReturnsIncompleteWorkflows()
        {
            var workflows = new[] { new UIWorkflow(), new UIWorkflow() };
            _workflowInfoService.GetIncompleteWorkflows(_connection, 25).Returns(workflows);

            var result = (OkObjectResult)_controller.List(_connection.Id, 25);

            Assert.That(result.Value, Is.SameAs(workflows));
        }

        // --- Get ---

        [Test]
        public void Get_Returns404_WhenConnectionNotFound()
        {
            Assert.That(_controller.Get(Guid.NewGuid(), Guid.NewGuid()), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Get_Returns404_WhenWorkflowNotFound()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJson(workflowId).Returns((string?)null);

            Assert.That(_controller.Get(_connection.Id, workflowId), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Get_ReturnsWorkflowJsonAndSuspendedFlag()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJson(workflowId).Returns("{ json }");
            var workflow = new UIWorkflow { IsSuspended = true };
            _workflowInfoService.GetWorkflowInfoFromJson("{ json }").Returns(workflow);

            var result = (OkObjectResult)_controller.Get(_connection.Id, workflowId);

            var response = (WorkflowResponse)result.Value!;
            Assert.That(response.WorkflowJson, Is.EqualTo("{ json }"));
            Assert.That(response.IsSuspended, Is.True);
            Assert.That(response.Workflow, Is.SameAs(workflow));
        }

        [Test]
        public void Get_IncludesDefinitionGraph_WhenWorkflowHasQualifiedName()
        {
            var workflowId = Guid.NewGuid();
            _workflowStore.GetWorkflowAsJson(workflowId).Returns("{ json }");
            _workflowInfoService.GetWorkflowInfoFromJson("{ json }").Returns(new UIWorkflow { QualifiedName = "Test.Workflow" });
            _workflowStore.GetDefinitionByQualifiedName("Test.Workflow").Returns(new WorkflowDefinition { Graph = "digraph {}" });

            var result = (OkObjectResult)_controller.Get(_connection.Id, workflowId);

            Assert.That(((WorkflowResponse)result.Value!).Workflow!.WorkflowGraph, Is.EqualTo("digraph {}"));
        }

        // --- Suspend / Unsuspend / Delete ---

        [Test]
        public void Suspend_Returns404_WhenConnectionNotFound()
        {
            var result = _controller.Suspend(Guid.NewGuid(), new WorkflowIdsRequest { WorkflowIds = new[] { Guid.NewGuid() } });

            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Suspend_Returns400_WhenNoWorkflowIds()
        {
            Assert.That(_controller.Suspend(_connection.Id, new WorkflowIdsRequest()), Is.TypeOf<BadRequestObjectResult>());
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Suspend_SuspendsEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = _controller.Suspend(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) _workflowStore.Received(1).SuspendWorkflow(id);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Unsuspend_UnsuspendsEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = _controller.Unsuspend(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) _workflowStore.Received(1).UnsuspendWorkflow(id);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Delete_DeletesEachWorkflow(int count)
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

            var result = _controller.Delete(_connection.Id, new WorkflowIdsRequest { WorkflowIds = ids });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            foreach (var id in ids) _workflowStore.Received(1).Delete(id);
        }

        // --- Definition ---

        [Test]
        public void Definition_Returns400_WhenNoQualifiedName()
        {
            Assert.That(_controller.Definition(_connection.Id, " "), Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public void Definition_Returns404_WhenConnectionNotFound()
        {
            Assert.That(_controller.Definition(Guid.NewGuid(), "A"), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Definition_Returns404_WhenDefinitionNotFound()
        {
            _workflowStore.GetDefinitionByQualifiedName("A").Returns((WorkflowDefinition?)null);

            Assert.That(_controller.Definition(_connection.Id, "A"), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Definition_ReturnsDefinition()
        {
            var definition = new WorkflowDefinition { QualifiedName = "A", Graph = "digraph {}" };
            _workflowStore.GetDefinitionByQualifiedName("A").Returns(definition);

            var result = (OkObjectResult)_controller.Definition(_connection.Id, "A");

            Assert.That(result.Value, Is.SameAs(definition));
        }
    }
}
