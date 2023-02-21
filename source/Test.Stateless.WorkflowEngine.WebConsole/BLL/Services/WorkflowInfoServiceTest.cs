using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
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
    }
}
