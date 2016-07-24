using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class WorkflowStoreInfoServiceTest
    {
        private IWorkflowStoreInfoService _workflowStoreInfoService;
        private IWorkflowClientFactory _workflowClientFactory;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _workflowClientFactory = Substitute.For<IWorkflowClientFactory>();

            _workflowStoreInfoService = new WorkflowStoreInfoService(_workflowClientFactory);
        }

        #region PopulateWorkflowStoreInfo Tests

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void PopulateWorkflowStoreInfo_ModelIsNull_ThrowsException()
        {
            _workflowStoreInfoService.PopulateWorkflowStoreInfo(null);
        }

        [Test]
        public void PopulateWorkflowStoreInfo_ClientErrorOccurs_PopulatesModelWithException()
        {
            const string exceptionMessage = "test exception";
            ConnectionModel connectionModel = new ConnectionModel();
            WorkflowStoreModel model = new WorkflowStoreModel(connectionModel);

            IWorkflowClient workflowClient = Substitute.For<IWorkflowClient>();
            workflowClient.When(x => x.GetActiveCount()).Do((ci) => { throw new Exception(exceptionMessage); });
            _workflowClientFactory.GetWorkflowClient(connectionModel).Returns(workflowClient);

            _workflowStoreInfoService.PopulateWorkflowStoreInfo(model);

            Assert.AreEqual(exceptionMessage, model.ConnectionError);
            Assert.IsNull(model.ActiveCount);
        }

        [Test]
        public void PopulateWorkflowStoreInfo_NoClientErrorOccurs_PopulatesModelWithCounts()
        {
            Random r = new Random();
            long activeCount = r.Next(11, 1000);
            long suspendedCount = r.Next(1, 10);
            long completedCount = r.Next(1001, 10000);

            ConnectionModel connectionModel = new ConnectionModel();
            WorkflowStoreModel model = new WorkflowStoreModel(connectionModel);

            IWorkflowClient workflowClient = Substitute.For<IWorkflowClient>();
            workflowClient.GetActiveCount().Returns(activeCount);
            workflowClient.GetSuspendedCount().Returns(suspendedCount);
            workflowClient.GetCompletedCount().Returns(completedCount);
            _workflowClientFactory.GetWorkflowClient(connectionModel).Returns(workflowClient);

            _workflowStoreInfoService.PopulateWorkflowStoreInfo(model);

            Assert.AreEqual(activeCount, model.ActiveCount);
            Assert.AreEqual(completedCount, model.CompletedCount);
            Assert.AreEqual(suspendedCount, model.SuspendedCount);
        }

        #endregion
    }
}
