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
using System.Threading.Tasks;

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

        #region IsSingleInstanceWorkflowRegisteredAsync Tests

        [Test]
        public async Task IsSingleInstanceWorkflowRegisteredAsync_WorkflowNotRegistered_ReturnsFalse()
        {
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = await regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSingleInstanceWorkflowRegisteredAsync_WorkflowRegisteredNotSingleInstance_ThrowsException()
        {
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSingleInstance = false;
            workflowStore.Save(workflow);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            Assert.ThrowsAsync<WorkflowException>(async () => await regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore));
        }

        [Test]
        public async Task IsSingleInstanceWorkflowRegisteredAsync_WorkflowRegistered_ReturnsTrue()
        {
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSingleInstance = true;
            workflowStore.Save(workflow);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = await regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore);
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsSingleInstanceWorkflowRegisteredAsync_OnExecution_ChecksByQualifiedName()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetAllByQualifiedNameAsync<BasicWorkflow>().Returns(Task.FromResult<IEnumerable<BasicWorkflow>>(new List<BasicWorkflow>()));

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            bool result = await regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore);

            await workflowStore.Received(1).GetAllByQualifiedNameAsync<BasicWorkflow>();
        }

        #endregion

        #region RegisterWorkflowAsync Tests

        [Test]
        public void RegisterWorkflowAsync_SingleInstanceWorkflowRegistered_ThrowsExceptionIfAlreadyExists()
        {
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            workflowStore.Save(new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start));

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            Assert.ThrowsAsync<SingleInstanceWorkflowAlreadyExistsException>(async () => await regService.RegisterWorkflowAsync(workflowStore, workflow));
        }

        [Test]
        public async Task RegisterWorkflowAsync_SingleInstanceWorkflowRegistered_RegistersIfDoesNotAlreadyExist()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetAllByQualifiedNameAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<Workflow>>(new List<Workflow>()));
            workflowStore.SaveAsync(Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            SingleInstanceWorkflow workflow = new SingleInstanceWorkflow(SingleInstanceWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            await regService.RegisterWorkflowAsync(workflowStore, workflow);

            await workflowStore.Received(1).GetAllByQualifiedNameAsync(workflow.QualifiedName);
            await workflowStore.Received(1).SaveAsync(workflow);
        }

        [Test]
        public async Task RegisterWorkflowAsync_MultipleInstanceWorkflowRegistered_Registers()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.SaveAsync(Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);

            IWorkflowRegistrationService regService = new WorkflowRegistrationService();
            await regService.RegisterWorkflowAsync(workflowStore, workflow);

            await workflowStore.DidNotReceive().GetAllByQualifiedNameAsync(Arg.Any<string>());
            await workflowStore.Received(1).SaveAsync(workflow);
        }

        #endregion


    }
}
