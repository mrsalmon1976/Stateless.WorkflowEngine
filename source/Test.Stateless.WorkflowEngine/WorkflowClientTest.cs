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
using Stateless.WorkflowEngine.Exceptions;
using System.Threading.Tasks;

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
            workflowStore.GetOrDefault(workflowId).Returns(workflow);
            
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = workflowClient.Exists(workflowId);

            workflowStore.Received(1).GetOrDefault(workflowId);
            Assert.That(result, Is.True);
        }

        [Test]
        public void Exists_WorkflowDoesNotExist_ReturnsFalse()
        {
            Guid workflowId = Guid.NewGuid();
            Workflow workflow = null;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetOrDefault(workflowId).Returns(workflow);

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = workflowClient.Exists(workflowId);

            workflowStore.Received(1).GetOrDefault(workflowId);
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

        #region DeleteAsync Tests

        [Test]
        public async Task DeleteAsync_OnExecute_RemovesFromStore()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.DeleteAsync(workflowId).Returns(Task.CompletedTask);

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            await workflowClient.DeleteAsync(workflowId);

            await workflowStore.Received(1).DeleteAsync(workflowId);
        }

        #endregion

        #region ExistsAsync Tests

        [Test]
        public async Task ExistsAsync_WorkflowExists_ReturnsTrue()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(workflow));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = await workflowClient.ExistsAsync(workflowId);

            await workflowStore.Received(1).GetOrDefaultAsync(workflowId);
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ExistsAsync_WorkflowDoesNotExist_ReturnsFalse()
        {
            Guid workflowId = Guid.NewGuid();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(null));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            bool result = await workflowClient.ExistsAsync(workflowId);

            await workflowStore.Received(1).GetOrDefaultAsync(workflowId);
            Assert.That(result, Is.False);
        }

        #endregion

        #region GetAsync Tests

        [Test]
        public async Task GetAsync_OnExecute_UsesStore()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(workflow));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            BasicWorkflow result = await workflowClient.GetAsync<BasicWorkflow>(workflowId);

            await workflowStore.Received(1).GetOrDefaultAsync(workflowId);
            Assert.That(result.Id, Is.EqualTo(workflowId));
        }

        [Test]
        public void GetAsync_WorkflowNotFound_ThrowsException()
        {
            Guid workflowId = Guid.NewGuid();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(null));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            Assert.ThrowsAsync<WorkflowNotFoundException>(async () => await workflowClient.GetAsync<BasicWorkflow>(workflowId));
        }

        #endregion

        #region GetIncompleteCountAsync Tests

        [Test]
        public async Task GetIncompleteCountAsync_OnExecute_UsesStore()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetIncompleteCountAsync().Returns(Task.FromResult(7L));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            long result = await workflowClient.GetIncompleteCountAsync();

            await workflowStore.Received(1).GetIncompleteCountAsync();
            Assert.That(result, Is.EqualTo(7L));
        }

        #endregion

        #region GetCompletedCountAsync Tests

        [Test]
        public async Task GetCompletedCountAsync_OnExecute_UsesStore()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetCompletedCountAsync().Returns(Task.FromResult(3L));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            long result = await workflowClient.GetCompletedCountAsync();

            await workflowStore.Received(1).GetCompletedCountAsync();
            Assert.That(result, Is.EqualTo(3L));
        }

        #endregion

        #region GetSuspendedCountAsync Tests

        [Test]
        public async Task GetSuspendedCountAsync_OnExecute_UsesStore()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetSuspendedCountAsync().Returns(Task.FromResult(2L));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            long result = await workflowClient.GetSuspendedCountAsync();

            await workflowStore.Received(1).GetSuspendedCountAsync();
            Assert.That(result, Is.EqualTo(2L));
        }

        #endregion

        #region IsSingleInstanceWorkflowRegisteredAsync Tests

        [Test]
        public async Task IsSingleInstanceWorkflowRegisteredAsync_OnExecute_UsesService()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();
            regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore).Returns(Task.FromResult(false));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService, Substitute.For<ICommandFactory>());
            await workflowClient.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>();

            await regService.Received(1).IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore);
        }

        #endregion

        #region RegisterAsync Tests

        [Test]
        public async Task RegisterAsync_OnRegister_UsesService()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();
            regService.RegisterWorkflowAsync(workflowStore, Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService, Substitute.For<ICommandFactory>());
            await workflowClient.RegisterAsync(workflow);

            await regService.Received(1).RegisterWorkflowAsync(workflowStore, workflow);
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

        #region UnsuspendAsync Tests

        [Test]
        public async Task UnsuspendAsync_OnExecute_CallsStore()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.UnsuspendWorkflowAsync(workflowId).Returns(Task.CompletedTask);
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(workflow));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            await workflowClient.UnsuspendAsync(workflowId);

            await workflowStore.Received(1).UnsuspendWorkflowAsync(workflowId);
        }

        [Test]
        public async Task UnsuspendAsync_OnExecute_ReturnsWorkflow()
        {
            Guid workflowId = Guid.NewGuid();
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.UnsuspendWorkflowAsync(workflowId).Returns(Task.CompletedTask);
            workflowStore.GetOrDefaultAsync(workflowId).Returns(Task.FromResult<Workflow>(workflow));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<ICommandFactory>());
            Workflow result = await workflowClient.UnsuspendAsync(workflowId);

            Assert.That(result.Id, Is.EqualTo(workflowId));
        }

        #endregion

    }
}
