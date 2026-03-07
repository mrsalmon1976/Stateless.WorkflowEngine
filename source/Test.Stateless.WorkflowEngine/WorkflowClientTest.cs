using System;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NSubstitute;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Stateless.WorkflowEngine.Services;
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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
            
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());

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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService);
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
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService);
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
            Assert.ThrowsAsync<WorkflowNotFoundException>(async () => await workflowClient.GetAsync<BasicWorkflow>(workflowId));
        }

        #endregion

        #region GetIncompleteCountAsync Tests

        [Test]
        public async Task GetIncompleteCountAsync_OnExecute_UsesStore()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.GetIncompleteCountAsync().Returns(Task.FromResult(7L));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
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

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService);
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
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, regService);
            await workflowClient.RegisterAsync(workflow);

            await regService.Received(1).RegisterWorkflowAsync(workflowStore, workflow);
        }

        #endregion

        #region Unsuspend Tests

        [Test]
        public void Unsuspend_OnExecute_SetsProperties()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            Workflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.MinValue;
            workflowStore.GetOrDefault(workflow.Id).Returns(workflow);

            DateTime dt = DateTime.UtcNow;


            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
            Workflow workflowResult = workflowClient.Unsuspend(workflow.Id);

            Assert.That(workflowResult, Is.Not.Null);
            Assert.That(workflowResult.Id, Is.EqualTo(workflow.Id));
            Assert.That(workflowResult.IsSuspended, Is.False);
            Assert.That(workflowResult.ResumeOn, Is.GreaterThanOrEqualTo(dt));

            workflowStore.Received(1).GetOrDefault(workflow.Id);
        }

        [Test]
        public void Unsuspend_OnExecute_SavesWorkflow()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            Workflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.MinValue;
            workflowStore.GetOrDefault(workflow.Id).Returns(workflow);

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
            Workflow workflowResult = workflowClient.Unsuspend(workflow.Id);

            workflowStore.Received(1).GetOrDefault(workflow.Id);
            workflowStore.Received(1).Save(workflow);
        }

        #endregion

        #region UnsuspendAsync Tests


        [Test]
        public async Task UnsuspendAsync_OnExecute_SetsProperties()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            Workflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.MinValue;
            workflowStore.GetOrDefaultAsync(workflow.Id).Returns(Task.FromResult(workflow));

            DateTime dt = DateTime.UtcNow;


            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
            Workflow workflowResult = await workflowClient.UnsuspendAsync(workflow.Id);

            Assert.That(workflowResult, Is.Not.Null);
            Assert.That(workflowResult.Id, Is.EqualTo(workflow.Id));
            Assert.That(workflowResult.IsSuspended, Is.False);
            Assert.That(workflowResult.ResumeOn, Is.GreaterThanOrEqualTo(dt));

            await workflowStore.Received(1).GetOrDefaultAsync(workflow.Id);
        }

        [Test]
        public async Task UnsuspendAsync_OnExecute_SavesWorkflow()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            Workflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.MinValue;
            workflowStore.GetOrDefaultAsync(workflow.Id).Returns(Task.FromResult(workflow));

            IWorkflowClient workflowClient = new WorkflowClient(workflowStore, Substitute.For<IWorkflowRegistrationService>());
            Workflow workflowResult = await workflowClient.UnsuspendAsync(workflow.Id);

            await workflowStore.Received(1).GetOrDefaultAsync(workflow.Id);
            await workflowStore.Received(1).SaveAsync(workflow);
        }

        #endregion

    }
}
