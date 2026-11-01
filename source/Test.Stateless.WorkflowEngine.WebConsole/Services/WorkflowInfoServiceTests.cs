using Newtonsoft.Json;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Services
{
    [TestFixture]
    public class WorkflowInfoServiceTests
    {
        private IWorkflowStoreFactory _workflowStoreFactory = null!;
        private IWorkflowStore _workflowStore = null!;
        private WorkflowInfoService _service = null!;
        private ConnectionModel _connection = null!;

        [SetUp]
        public void SetUp()
        {
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();
            _workflowStore = Substitute.For<IWorkflowStore>();
            _connection = TestHelpers.CreateConnection();
            _workflowStoreFactory.GetWorkflowStore(_connection).Returns(_workflowStore);
            _service = new WorkflowInfoService(_workflowStoreFactory);
        }

        // --- GetWorkflowStoreInfo ---

        [Test]
        public void GetWorkflowStoreInfo_ReturnsCounts()
        {
            _workflowStore.GetActiveCount().Returns(10);
            _workflowStore.GetSuspendedCount().Returns(2);
            _workflowStore.GetCompletedCount().Returns(100);

            var info = _service.GetWorkflowStoreInfo(_connection);

            Assert.That(info.ActiveCount, Is.EqualTo(10));
            Assert.That(info.SuspendedCount, Is.EqualTo(2));
            Assert.That(info.CompleteCount, Is.EqualTo(100));
            Assert.That(info.ConnectionError, Is.Null);
        }

        [Test]
        public void GetWorkflowStoreInfo_CapturesError_WhenStoreFails()
        {
            _workflowStore.GetActiveCount().Returns(x => throw new Exception("boom"));

            var info = _service.GetWorkflowStoreInfo(_connection);

            Assert.That(info.ConnectionError, Is.EqualTo("boom"));
        }

        // --- GetIncompleteWorkflows ---

        [Test]
        public void GetIncompleteWorkflows_LoadsEachDefinitionOnce()
        {
            var docs = new[]
            {
                CreateWorkflowJson("A", "Test.WorkflowA"),
                CreateWorkflowJson("A", "Test.WorkflowA"),
                CreateWorkflowJson("B", "Test.WorkflowB")
            };
            _workflowStore.GetIncompleteWorkflowsAsJson(50).Returns(docs);
            _workflowStore.GetDefinitionByQualifiedName("Test.WorkflowA").Returns(new WorkflowDefinition { Graph = "graphA" });
            _workflowStore.GetDefinitionByQualifiedName("Test.WorkflowB").Returns((WorkflowDefinition?)null);

            var result = _service.GetIncompleteWorkflows(_connection, 50).ToList();

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0].WorkflowGraph, Is.EqualTo("graphA"));
            Assert.That(result[1].WorkflowGraph, Is.EqualTo("graphA"));
            Assert.That(result[2].WorkflowGraph, Is.Null);
            _workflowStore.Received(1).GetDefinitionByQualifiedName("Test.WorkflowA");
            _workflowStore.Received(1).GetDefinitionByQualifiedName("Test.WorkflowB");
        }

        [Test]
        public void GetIncompleteWorkflows_DoesNotLoadDefinition_WhenNoQualifiedName()
        {
            _workflowStore.GetIncompleteWorkflowsAsJson(5).Returns(new[] { CreateWorkflowJson("A", null) });

            var result = _service.GetIncompleteWorkflows(_connection, 5).ToList();

            Assert.That(result, Has.Count.EqualTo(1));
            _workflowStore.DidNotReceiveWithAnyArgs().GetDefinitionByQualifiedName(default!);
        }

        // --- GetWorkflowInfoFromJson ---

        [Test]
        public void GetWorkflowInfoFromJson_ParsesWorkflowAndIgnoresExtraElements()
        {
            var id = Guid.NewGuid();
            string json = JsonConvert.SerializeObject(new
            {
                _id = id,
                WorkflowType = "Test.MyWorkflow, Test",
                Workflow = new { Id = id, Name = "MyWorkflow", CurrentState = "Start", IsSuspended = true, RetryCount = 3, CustomProperty = "x" }
            });

            var wf = _service.GetWorkflowInfoFromJson(json);

            Assert.That(wf.Name, Is.EqualTo("MyWorkflow"));
            Assert.That(wf.CurrentState, Is.EqualTo("Start"));
            Assert.That(wf.IsSuspended, Is.True);
            Assert.That(wf.RetryCount, Is.EqualTo(3));
        }

        [Test]
        public void GetWorkflowInfoFromJson_ParsesLegacyGuidId_FromShellJson()
        {
            // format returned by IWorkflowStore.GetWorkflowAsJson (workflow detail)
            var id = Guid.NewGuid();
            string json = "{ \"_id\" : CSUUID(\"" + id + "\"), \"Workflow\" : { \"_id\" : CSUUID(\"" + id + "\"), \"Name\" : \"MyWorkflow\" } }";

            var wf = _service.GetWorkflowInfoFromJson(json);

            Assert.That(wf.Id, Is.EqualTo(id));
        }

        [Test]
        public void GetWorkflowInfoFromJson_ParsesLegacyGuidId_FromRelaxedJson()
        {
            // format returned by IWorkflowStore.GetIncompleteWorkflowsAsJson (workflow list) - binary subtype 3 is the C#
            // legacy representation, so the bytes are in Guid.ToByteArray() order
            var id = Guid.NewGuid();
            string binary = "{ \"$binary\" : { \"base64\" : \"" + Convert.ToBase64String(id.ToByteArray()) + "\", \"subType\" : \"03\" } }";
            string json = "{ \"_id\" : " + binary + ", \"Workflow\" : { \"_id\" : " + binary + ", \"Name\" : \"MyWorkflow\" } }";

            var wf = _service.GetWorkflowInfoFromJson(json);

            Assert.That(wf.Id, Is.EqualTo(id));
        }

        [Test]
        public void GetWorkflowInfoFromJson_DerivesName_WhenMissing()
        {
            string json = JsonConvert.SerializeObject(new
            {
                WorkflowType = "My.Namespace.LegacyWorkflow, My.Assembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                Workflow = new { CurrentState = "Start" }
            });

            var wf = _service.GetWorkflowInfoFromJson(json);

            Assert.That(wf.Name, Is.EqualTo("LegacyWorkflow"));
        }

        private static string CreateWorkflowJson(string name, string? qualifiedName)
        {
            return JsonConvert.SerializeObject(new
            {
                WorkflowType = "Test." + name,
                Workflow = new { Id = Guid.NewGuid(), Name = name, QualifiedName = qualifiedName, CurrentState = "Start" }
            });
        }
    }
}
