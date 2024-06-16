using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NSubstitute;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Commands;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowClientTest
    {
        #region Delete Tests

        [Test]
        public void Delete_OnExecute_RemovesFromStore()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            workflowClient.Delete(workflowId);

            workflowStore.Received(1).Delete(workflowId);
        }

        #endregion

        #region Exists Tests

        [Test]
        public void Exists_WorkflowExists_ReturnsTrue()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.Get(workflowId).Returns(workflow);
            
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = workflowClient.Exists(workflowId);

            workflowStore.Received(1).Get(workflowId);
            Assert.That(result, Is.True);
        }

        [Test]
        public void Exists_WorkflowDoesNotExist_ReturnsFalse()
        {
            Guid workflowId = Guid.NewGuid();
            Workflow workflow = null;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.Get(workflowId).Returns(workflow);

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = workflowClient.Exists(workflowId);

            workflowStore.Received(1).Get(workflowId);
            Assert.That(result, Is.False);
        }

        #endregion

        #region Get Tests

        [Test]
        public void Get_OnExecute_UsesStore()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());

            BasicWorkflow workflow = workflowClient.Get<BasicWorkflow>(workflowId);
            workflowStore.Received(1).Get<BasicWorkflow>(workflowId);
        }

        #endregion

        #region IsSingleInstanceWorkflowRegistered Tests

        [Test]
        public void IsSingleInstanceWorkflowRegistered_OnExecute_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService, Substitute.For<ICommandFactory>());
            workflowClient.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();
            regService.Received(1).IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
        }

        #endregion

        #region Register Tests

        [Test]
        public void Register_OnRegister_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService, Substitute.For<ICommandFactory>());
            workflowClient.Register(workflow);

            regService.Received(1).RegisterWorkflow(workflowStore, workflow);

        }

        #endregion

        #region Unsuspend Tests

        [Test]
        public void Unsuspend_OnExecute_SetsProperties()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            UnsuspendWorkflowCommand cmd = Substitute.For<UnsuspendWorkflowCommand>();

            ICommandFactory commandFactory = Substitute.For<ICommandFactory>();
            commandFactory.CreateCommand<UnsuspendWorkflowCommand>().Returns(cmd);
            
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), commandFactory);
            workflowClient.Unsuspend(workflowId);

            cmd.Received(1).WorkflowId = workflowId;
            cmd.Received(1).WorkflowStore = workflowStore;
            cmd.Received(1).Execute();
        }

        [Test]
        public void Unsuspend_OnExecute_ReturnsWorkflow()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            UnsuspendWorkflowCommand cmd = Substitute.For<UnsuspendWorkflowCommand>();
            cmd.Execute().Returns(workflow);

            ICommandFactory commandFactory = Substitute.For<ICommandFactory>();
            commandFactory.CreateCommand<UnsuspendWorkflowCommand>().Returns(cmd);

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), commandFactory);
            BasicWorkflow result = (BasicWorkflow)workflowClient.Unsuspend(workflowId);
            Assert.That(result.Id, Is.EqualTo(workflowId));

        }

        #endregion

    }
}
