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

        // --- GetWorkflowStoreInfoAsync ---

        [Test]
        public async Task GetWorkflowStoreInfo_ReturnsCounts()
        {
            _workflowStore.GetActiveCountAsync().Returns(Task.FromResult(10L));
            _workflowStore.GetSuspendedCountAsync().Returns(Task.FromResult(2L));
            _workflowStore.GetCompletedCountAsync().Returns(Task.FromResult(100L));

            var info = await _service.GetWorkflowStoreInfoAsync(_connection);

            Assert.That(info.ActiveCount, Is.EqualTo(10));
            Assert.That(info.SuspendedCount, Is.EqualTo(2));
            Assert.That(info.CompleteCount, Is.EqualTo(100));
            Assert.That(info.ConnectionError, Is.Null);
        }

        [Test]
        public async Task GetWorkflowStoreInfo_CapturesError_WhenStoreFails()
        {
            _workflowStore.GetActiveCountAsync().Returns(Task.FromException<long>(new Exception("boom")));

            var info = await _service.GetWorkflowStoreInfoAsync(_connection);

            Assert.That(info.ConnectionError, Is.EqualTo("boom"));
        }

        // --- GetIncompleteWorkflowsAsync ---

        [Test]
        public async Task GetIncompleteWorkflows_LoadsEachDefinitionOnce()
        {
            var docs = new[]
            {
                CreateWorkflowJson("A", "Test.WorkflowA"),
                CreateWorkflowJson("A", "Test.WorkflowA"),
                CreateWorkflowJson("B", "Test.WorkflowB")
            };
            _workflowStore.GetIncompleteWorkflowsAsJsonAsync(50).Returns(Task.FromResult<IEnumerable<string>>(docs));
            _workflowStore.GetDefinitionByQualifiedNameAsync("Test.WorkflowA").Returns(Task.FromResult<WorkflowDefinition?>(new WorkflowDefinition { Graph = "graphA" }));
            _workflowStore.GetDefinitionByQualifiedNameAsync("Test.WorkflowB").Returns(Task.FromResult<WorkflowDefinition?>(null));

            var result = (await _service.GetIncompleteWorkflowsAsync(_connection, 50)).ToList();

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0].WorkflowGraph, Is.EqualTo("graphA"));
            Assert.That(result[1].WorkflowGraph, Is.EqualTo("graphA"));
            Assert.That(result[2].WorkflowGraph, Is.Null);
            await _workflowStore.Received(1).GetDefinitionByQualifiedNameAsync("Test.WorkflowA");
            await _workflowStore.Received(1).GetDefinitionByQualifiedNameAsync("Test.WorkflowB");
        }

        [Test]
        public async Task GetIncompleteWorkflows_DoesNotLoadDefinition_WhenNoQualifiedName()
        {
            _workflowStore.GetIncompleteWorkflowsAsJsonAsync(5).Returns(Task.FromResult<IEnumerable<string>>(new[] { CreateWorkflowJson("A", null) }));

            var result = (await _service.GetIncompleteWorkflowsAsync(_connection, 5)).ToList();

            Assert.That(result, Has.Count.EqualTo(1));
            await _workflowStore.DidNotReceiveWithAnyArgs().GetDefinitionByQualifiedNameAsync(default!);
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

            var wf = _service.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, json);

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

            var wf = _service.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, json);

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

            var wf = _service.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, json);

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

            var wf = _service.GetWorkflowInfoFromJson(WorkflowStoreType.MongoDb, json);

            Assert.That(wf.Name, Is.EqualTo("LegacyWorkflow"));
        }

        [Test]
        public void GetWorkflowInfoFromJson_ParsesRavenDbDocument()
        {
            // format returned by RavenDbWorkflowStore - the workflow type can't be loaded, so $type must be ignored
            var id = Guid.NewGuid();
            string json = "{ \"Id\": \"Workflow/" + id + "\", \"WorkflowType\": \"Test.MyWorkflow, Test\", "
                + "\"Workflow\": { \"$type\": \"Test.MyWorkflow, Test\", \"Id\": \"" + id + "\", \"Name\": \"MyWorkflow\", \"CurrentState\": \"Start\", "
                + "\"IsSuspended\": true, \"RetryCount\": 2, \"RetryIntervals\": [5, 10], \"CreatedOn\": \"2026-11-01T10:15:30.0000000Z\", \"CustomProperty\": \"x\" }, "
                + "\"@metadata\": { \"@collection\": \"RavenWorkflows\", \"@id\": \"Workflow/" + id + "\", \"Raven-Clr-Type\": \"Stateless.WorkflowEngine.RavenDb.RavenWorkflow, Stateless.WorkflowEngine.RavenDb\" } }";

            var wf = _service.GetWorkflowInfoFromJson(WorkflowStoreType.RavenDb, json);

            Assert.That(wf.Id, Is.EqualTo(id));
            Assert.That(wf.Name, Is.EqualTo("MyWorkflow"));
            Assert.That(wf.CurrentState, Is.EqualTo("Start"));
            Assert.That(wf.IsSuspended, Is.True);
            Assert.That(wf.RetryCount, Is.EqualTo(2));
            Assert.That(wf.RetryIntervals, Is.EqualTo(new[] { 5, 10 }));
            Assert.That(wf.CreatedOn, Is.EqualTo(new DateTime(2026, 11, 1, 10, 15, 30, DateTimeKind.Utc)));
        }

        [Test]
        public async Task GetIncompleteWorkflows_ParsesDocumentsForConnectionStoreType()
        {
            var connection = TestHelpers.CreateRavenDbConnection();
            _workflowStoreFactory.GetWorkflowStore(connection).Returns(_workflowStore);
            // not valid for the MongoDb parser, which treats $type as an extended json type
            string json = "{ \"Workflow\": { \"$type\": \"Test.MyWorkflow, Test\", \"Name\": \"MyWorkflow\" } }";
            _workflowStore.GetIncompleteWorkflowsAsJsonAsync(5).Returns(Task.FromResult<IEnumerable<string>>(new[] { json }));

            var result = (await _service.GetIncompleteWorkflowsAsync(connection, 5)).ToList();

            Assert.That(result.Single().Name, Is.EqualTo("MyWorkflow"));
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
