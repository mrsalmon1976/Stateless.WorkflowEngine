using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Test.Stateless.WorkflowEngine.Workflows.SingleInstance;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Services;
using NSubstitute;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowRegistrationServiceTest
    {

        #region IsSingleInstanceWorkflowRegistered Tests

        [Test]
        public void IsSingleInstanceWorkflowRegistered_WorkflowNotRegistered_ReturnsFalse()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            // execute
            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = regService.IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
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
            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = regService.IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);

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
            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = regService.IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
            Assert.IsTrue(result);

        }

        #endregion

        #region RegisterWorkflow Tests

        [Test]
        [ExpectedException(ExpectedException = typeof(SingleInstanceWorkflowAlreadyExistsException))]
        public void RegisterWorkflow_SingleInstanceWorkflowRegistered_ThrowsExceptionIfAlreadyExists()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            workflowStore.Save(new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start));

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            regService.RegisterWorkflow(workflowStore, workflow);
           
        }

        [Test]
        public void RegisterWorkflow_SingleInstanceWorkflowRegistered_RegistersIfDoesNotAlreadyExist()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = MockUtils.CreateAndRegister<IWorkflowStore>();
            workflowStore.GetAllByType(Arg.Any<string>()).Returns(new List<Workflow>());

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            regService.RegisterWorkflow(workflowStore, workflow);

            workflowStore.Received(1).GetAllByType(workflow.GetType().AssemblyQualifiedName);

        }

        [Test]
        public void RegisterWorkflow_MultipleInstanceWorkflowRegistered_Registers()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = MockUtils.CreateAndRegister<IWorkflowStore>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            regService.RegisterWorkflow(workflowStore, workflow);

            workflowStore.DidNotReceive().GetAllByType(Arg.Any<string>());

        }

        #endregion


    }
}
