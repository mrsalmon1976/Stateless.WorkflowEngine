using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using NSubstitute;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SingleInstance;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Services;
using StructureMap;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowClientTest
    {

        #region IsSingleInstanceWorkflowRegistered Tests

        [Test]
        public void IsSingleInstanceWorkflowRegistered_WorkflowNotRegistered_ReturnsFalse()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            // execute
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            bool result = workflowClient.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();
            Assert.IsFalse(result);

        }

        [Test]
        [ExpectedException(ExpectedException = typeof(WorkflowException))]
        public void IsSingleInstanceWorkflowRegistered_WorkflowRegisteredNotSingleInstance_ThrowsException()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSingleInstance = false;
            workflowStore.Save(workflow);

            // execute
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            workflowClient.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();

        }

        [Test]
        public void IsSingleInstanceWorkflowRegistered_WorkflowRegistered_ReturnsTrue()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSingleInstance = true;
            workflowStore.Save(workflow);

            // execute
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            bool result = workflowClient.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();
            Assert.IsTrue(result);

        }

        #endregion

        #region RegisterWorkflow Tests

        [Test]
        public void RegisterWorkflow_OnRegister_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = MockUtils.CreateAndRegister<IWorkflowStore>();
            IWorkflowRegistrationService regService = MockUtils.CreateAndRegister<IWorkflowRegistrationService>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflow(workflow);

            regService.Received(1).RegisterWorkflow(workflowStore, workflow);

        }

        #endregion

    }
}
