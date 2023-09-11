using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class WorkflowInfoServiceTest
    {
        private IWorkflowInfoService _workflowInfoService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _workflowStoreFactory = Substitute.For<IWorkflowStoreFactory>();

            _workflowInfoService = new WorkflowInfoService(_workflowStoreFactory);
        }

        #region GetWorkflowStoreInfo Tests

        [Test]
        public void GetWorkflowStoreInfo_ModelIsNull_ThrowsException()
        {
            TestDelegate del = () => _workflowInfoService.GetWorkflowStoreInfo(null);
            // assert
            Assert.Throws<ArgumentNullException>(del);
        }

        [Test]
        public void GetWorkflowStoreInfo_ClientErrorOccurs_PopulatesModelWithException()
        {
            const string exceptionMessage = "test exception";
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.When(x => x.GetActiveCount()).Do((ci) => { throw new Exception(exceptionMessage); });
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            ConnectionInfoViewModel model = _workflowInfoService.GetWorkflowStoreInfo(connectionModel);

            Assert.AreEqual(exceptionMessage, model.ConnectionError);
            Assert.IsNull(model.ActiveCount);
        }

        [Test]
        public void GetWorkflowStoreInfo_NoClientErrorOccurs_PopulatesModelWithCounts()
        {
            Random r = new Random();
            long activeCount = r.Next(11, 1000);
            long suspendedCount = r.Next(1, 10);
            long completedCount = r.Next(1001, 10000);

            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetActiveCount().Returns(activeCount);
            workflowStore.GetSuspendedCount().Returns(suspendedCount);
            workflowStore.GetCompletedCount().Returns(completedCount);
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            ConnectionInfoViewModel model = _workflowInfoService.GetWorkflowStoreInfo(connectionModel);

            Assert.AreEqual(activeCount, model.ActiveCount);
            Assert.AreEqual(completedCount, model.CompleteCount);
            Assert.AreEqual(suspendedCount, model.SuspendedCount);
        }

        #endregion

        #region GetWorkflowDefinition Tests

        [Test]
        public void GetWorkflowDefinition_ModelIsNull_ThrowsException()
        {
            TestDelegate del = () => _workflowInfoService.GetWorkflowDefinition(null, "test");
            // assert
            Assert.Throws<ArgumentNullException>(del);
        }

        [Test]
        public void GetWorkflowStoreInfo_DefinitionDoesNotExist_ReturnssNull()
        {
            ConnectionModel connectionModel = new ConnectionModel();
            string qualifedWorkflowName = Guid.NewGuid().ToString();

            WorkflowDefinition workflowDefinition = null;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);
            workflowStore.GetDefinitionByQualifiedName(qualifedWorkflowName).Returns(workflowDefinition);

            string result = _workflowInfoService.GetWorkflowDefinition(connectionModel, qualifedWorkflowName);

            Assert.IsNull(result);
        }

        [Test]
        public void GetWorkflowStoreInfo_DefinitionExists_ReturnsGraphValue()
        {
            ConnectionModel connectionModel = new ConnectionModel();
            string qualifedWorkflowName = Guid.NewGuid().ToString();

            WorkflowDefinition workflowDefinition = new WorkflowDefinition();
            workflowDefinition.Graph = Guid.NewGuid().ToString();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);
            workflowStore.GetDefinitionByQualifiedName(qualifedWorkflowName).Returns(workflowDefinition);

            string result = _workflowInfoService.GetWorkflowDefinition(connectionModel, qualifedWorkflowName);

            Assert.IsNotNull(result);
            Assert.AreEqual(workflowDefinition.Graph, result);
        }

        #endregion

        #region GetIncompleteWorkflows Tests

        [Test]
        public void GetIncompleteWorkflows_GetsAllIncomplete()
        {
            // setup 
            int workflowCount = new Random().Next(1, 10);
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            IEnumerable<UIWorkflowContainer> workflows = CreateUiWorkflows(workflowCount);
            IEnumerable<string> workflowsJson = workflows.Select(x => JsonConvert.SerializeObject(x));
            workflowStore.GetIncompleteWorkflowsAsJson(workflowCount).Returns(workflowsJson);


            // execute
            IEnumerable<UIWorkflow> result = _workflowInfoService.GetIncompleteWorkflows(connectionModel, workflowCount);


            // assert
            Assert.That(result.Count(), Is.EqualTo(workflowCount));
            workflowStore.Received(1).GetIncompleteWorkflowsAsJson(workflowCount);
        }

        [Test]
        public void GetIncompleteWorkflows_WorkflowWithGraphData_GraphDataPopulated()
        {
            // setup 
            string graphData = Guid.NewGuid().ToString();
            const int workflowCount = 1;
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            List<UIWorkflowContainer> workflows = CreateUiWorkflows(workflowCount, workflowGraph: graphData);
            IEnumerable<string> workflowsJson = workflows.Select(x => JsonConvert.SerializeObject(x));
            workflowStore.GetIncompleteWorkflowsAsJson(workflowCount).Returns(workflowsJson);

            string qualifiedName = workflows[0].Workflow.QualifiedName;

            WorkflowDefinition workflowDefinition = new WorkflowDefinition();
            workflowDefinition.QualifiedName = qualifiedName;
            workflowDefinition.Graph = graphData;
            workflowStore.GetDefinitionByQualifiedName(qualifiedName).Returns(workflowDefinition);


            // execute
            IEnumerable<UIWorkflow> result = _workflowInfoService.GetIncompleteWorkflows(connectionModel, workflowCount);

            // assert
            Assert.AreEqual(workflowCount, result.Count());
            UIWorkflow wfResult = result.Single();
            Assert.AreEqual(graphData, wfResult.WorkflowGraph);
            workflowStore.Received(1).GetDefinitionByQualifiedName(qualifiedName);
        }

        [Test]
        public void GetIncompleteWorkflows_WorkflowWithoutGraphData_GraphDataSetToNull()
        {
            // setup 
            string graphData = Guid.NewGuid().ToString();
            const int workflowCount = 1;
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            List<UIWorkflowContainer> workflows = CreateUiWorkflows(workflowCount, workflowGraph: graphData);
            IEnumerable<string> workflowsJson = workflows.Select(x => JsonConvert.SerializeObject(x));
            workflowStore.GetIncompleteWorkflowsAsJson(workflowCount).Returns(workflowsJson);

            string qualifiedName = workflows[0].Workflow.QualifiedName;

            // execute
            IEnumerable<UIWorkflow> result = _workflowInfoService.GetIncompleteWorkflows(connectionModel, workflowCount);

            // assert
            Assert.AreEqual(workflowCount, result.Count());
            UIWorkflow wfResult = result.Single();
            Assert.IsNull(wfResult.WorkflowGraph);
            workflowStore.Received(1).GetDefinitionByQualifiedName(qualifiedName);
        }

        [Test]
        public void GetIncompleteWorkflows_MultipleWorkflowsWithCommonGraphData_LocalCacheUsed()
        {
            // setup 
            string qualifiedName = Guid.NewGuid().ToString();
            string graphData = Guid.NewGuid().ToString();
            const int workflowCount = 2;
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            List<UIWorkflowContainer> workflows = CreateUiWorkflows(workflowCount, workflowGraph: graphData, qualifiedName: qualifiedName);
            IEnumerable<string> workflowsJson = workflows.Select(x => JsonConvert.SerializeObject(x));
            workflowStore.GetIncompleteWorkflowsAsJson(workflowCount).Returns(workflowsJson);

            WorkflowDefinition workflowDefinition = new WorkflowDefinition();
            workflowDefinition.QualifiedName = qualifiedName;
            workflowDefinition.Graph = graphData;
            workflowStore.GetDefinitionByQualifiedName(qualifiedName).Returns(workflowDefinition);


            // execute
            IEnumerable<UIWorkflow> result = _workflowInfoService.GetIncompleteWorkflows(connectionModel, workflowCount);

            // assert
            Assert.AreEqual(workflowCount, result.Count());
            foreach (UIWorkflow wf in result)
            {
                Assert.AreEqual(graphData, wf.WorkflowGraph);
            }
            workflowStore.Received(1).GetDefinitionByQualifiedName(qualifiedName);
        }

        [Test]
        public void GetIncompleteWorkflows_WorkflowWithNoQualifiedName_GraphDataNotPopulated()
        {
            // setup 
            string graphData = Guid.NewGuid().ToString();
            const int workflowCount = 1;
            ConnectionModel connectionModel = new ConnectionModel();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            _workflowStoreFactory.GetWorkflowStore(connectionModel).Returns(workflowStore);

            List<UIWorkflowContainer> workflows = CreateUiWorkflows(workflowCount, workflowGraph: graphData);
            List<string> workflowsJson = workflows.Select(x => JsonConvert.SerializeObject(x)).ToList();
            workflowsJson[0] = workflowsJson[0].Replace("QualifiedName", "REDACTED");
            workflowStore.GetIncompleteWorkflowsAsJson(workflowCount).Returns(workflowsJson);

            // execute
            IEnumerable<UIWorkflow> result = _workflowInfoService.GetIncompleteWorkflows(connectionModel, workflowCount);

            // assert
            Assert.AreEqual(workflowCount, result.Count());
            workflowStore.DidNotReceive().GetDefinitionByQualifiedName(Arg.Any<string>());
        }

        #endregion

        #region Private Methods

        private List<UIWorkflowContainer> CreateUiWorkflows(int count, string workflowGraph = null, string qualifiedName = null)
        {
            List<UIWorkflowContainer> workflows = new List<UIWorkflowContainer>();
            for (int i=0; i<count; i++)
            {
                UIWorkflow uIWorkflow = new UIWorkflow();
                uIWorkflow.Id = Guid.NewGuid();
                uIWorkflow.QualifiedName = (qualifiedName == null ? Guid.NewGuid().ToString() : qualifiedName);
                uIWorkflow.WorkflowGraph = workflowGraph;

                UIWorkflowContainer container = new UIWorkflowContainer();
                container.Workflow = uIWorkflow;

                workflows.Add(container);

            }
            return workflows;
        }

        #endregion

    }
}
