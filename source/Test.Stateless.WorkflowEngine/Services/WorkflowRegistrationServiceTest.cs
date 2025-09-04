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
            Assert.That(result, Is.False);

        }

        [Test]
        public void IsSingleInstanceWorkflowRegistered_WorkflowRegisteredNotSingleInstance_ThrowsException()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSingleInstance = false;
            workflowStore.Save(workflow);

            // execute
            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            TestDelegate del = () => regService.IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
            // assert
            Assert.Throws<WorkflowException>(del);

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
            Assert.That(result, Is.True);

        }

        [Test]
        public void IsSingleInstanceWorkflowRegistered_OnExecution_ChecksByQualifiedName()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            // execute
            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = regService.IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);

            // assert
            workflowStore.Received(1).GetAllByQualifiedName<BasicWorkflow>();

        }

        #endregion

        #region RegisterWorkflow Tests

        [Test]
        public void RegisterWorkflow_SingleInstanceWorkflowRegistered_ThrowsExceptionIfAlreadyExists()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            workflowStore.Save(new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start));

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            TestDelegate del = () => regService.RegisterWorkflow(workflowStore, workflow);
            Assert.Throws<SingleInstanceWorkflowAlreadyExistsException>(del);
        }

        [Test]
        public void RegisterWorkflow_SingleInstanceWorkflowRegistered_RegistersIfDoesNotAlreadyExist()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetAllByType(Arg.Any<string>()).Returns(new List<Workflow>());

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            regService.RegisterWorkflow(workflowStore, workflow);

            workflowStore.Received(1).GetAllByQualifiedName(workflow.GetType().FullName);
			workflowStore.Received(1).GetAllByQualifiedName(workflow.QualifiedName);

		}

        [Test]
        public void RegisterWorkflow_MultipleInstanceWorkflowRegistered_Registers()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            regService.RegisterWorkflow(workflowStore, workflow);

            workflowStore.DidNotReceive().GetAllByType(Arg.Any<string>());
			workflowStore.DidNotReceive().GetAllByQualifiedName(Arg.Any<string>());

		}

        #endregion


    }
}
