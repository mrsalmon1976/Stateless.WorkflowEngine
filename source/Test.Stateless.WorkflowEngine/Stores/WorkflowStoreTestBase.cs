using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
using Test.Stateless.WorkflowEngine.Workflows.SingleInstance;
using Stateless.WorkflowEngine.Models;

namespace Test.Stateless.WorkflowEngine.Stores
{
    public abstract class WorkflowStoreTestBase
    {

        #region Protected Methods

        /// <summary>
        /// Gets the store relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected abstract IWorkflowStore GetStore();

        #endregion

        #region Archive Tests

        [Test]
        public void Archive_WorkflowIsMoved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);

            store.Archive(wf);
            Assert.IsNull(store.GetOrDefault(wf.Id));
            Assert.IsNotNull(store.GetCompletedOrDefault(wf.Id));
        }

        #endregion

        #region Delete Tests

        [Test]
        public void Delete_OnExecute_RemovesWorkflow()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore store = GetStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;
            store.Save(workflow);

            Workflow result1 = store.GetOrDefault(workflowId);
            Assert.IsNotNull(result1);

            store.Delete(workflowId);
            Workflow result2 = store.GetOrDefault(workflowId);
            Assert.IsNull(result2);
        }

        #endregion

        #region Get Tests

        [Test]
        [ExpectedException(ExpectedException = typeof(WorkflowNotFoundException))]
        public void Get_InvalidId_ThrowsException()
        {
            IWorkflowStore store = GetStore();
            store.Get(Guid.NewGuid());
        }

        [Test]
        public void Get_ValidId_ReturnsWorkflow()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();

            store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start));

            Workflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);

            Workflow result = store.Get(wf.Id);
            Assert.IsNotNull(result);
            Assert.AreEqual(wf.Id, result.Id);
        }

        #endregion

        #region GetActive Tests

        [Test]
        public void GetActive_WorkflowIsSuspended_NotReturned()
        {
            // factory method for workflows
            Func<bool, BasicWorkflow> createWorkflow = (isSuspended) => {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.IsSuspended = isSuspended;
                return wf;
            };

            Workflow activeWorkflow = createWorkflow(false);
            Workflow suspendedWorkflow = createWorkflow(true);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(activeWorkflow);
            store.Save(suspendedWorkflow);

            // fetch the workflows, only one should be returned
            List<Workflow> workflows = store.GetActive(10).ToList();
            Assert.AreEqual(1, workflows.Count);
            Assert.AreEqual(activeWorkflow.Id, workflows[0].Id);
        }

        [Test]
        public void GetActive_WorkflowResumeDateInFuture_NotReturned()
        {
            // factory method for workflows
            Func<bool, DateTime, BasicWorkflow> createWorkflow = (isSuspended, resumeOn) => {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.IsSuspended = isSuspended;
                wf.ResumeOn = resumeOn;
                wf.BasicMetaData = Guid.NewGuid().ToString();
                return wf;
            };

            Workflow noResumeDateWorkflow = createWorkflow(false, DateTime.MinValue);
            Workflow resumeDateActiveWorkflow = createWorkflow(false, DateTime.UtcNow.AddMilliseconds(-1));
            Workflow futureDatedWorkflow = createWorkflow(false, DateTime.UtcNow.AddMinutes(3));

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(noResumeDateWorkflow);
            store.Save(resumeDateActiveWorkflow);
            store.Save(futureDatedWorkflow);

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetActive(10).ToList();
            Assert.AreEqual(2, workflows.Count);

            Assert.IsNotNull(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id));
            Assert.IsNotNull(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id));
            Assert.IsNull(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id));
        }

        [Test]
        public void GetActive_MultipleWorkflowsReturned_OrderedByRetryCountBeforeCreateDate()
        {
            // factory method for workflows
            Func<DateTime, int, BasicWorkflow> createWorkflow = (createdOn, retryCount) => {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.CreatedOn = createdOn;
                wf.RetryCount = retryCount;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;
            
            Workflow workflow1 = createWorkflow(baseDate.AddMinutes(1), 2);
            Workflow workflow2 = createWorkflow(baseDate.AddMinutes(-1), 1);
            Workflow workflow3 = createWorkflow(baseDate.AddMinutes(1), 3);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3 });

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetActive(10).ToList();

            Assert.AreEqual(workflow3.Id, workflows[0].Id);
            Assert.AreEqual(workflow1.Id, workflows[1].Id);
            Assert.AreEqual(workflow2.Id, workflows[2].Id);
        }

        [Test]
        public void GetActive_MultipleWorkflowsReturned_OrderedByCreateDateAfterRetryCount()
        {
            // factory method for workflows
            Func<DateTime, BasicWorkflow> createWorkflow = (createdOn) => {
                BasicWorkflow wf = new BasicWorkflow("Start");
                wf.CreatedOn = createdOn;
                wf.RetryCount = 1;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;

            Workflow workflow1 = createWorkflow(baseDate.AddMinutes(1));
            Workflow workflow2 = createWorkflow(baseDate.AddMinutes(-1));
            Workflow workflow3 = createWorkflow(baseDate.AddMinutes(2));

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3 });

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetActive(10).ToList();

            Assert.AreEqual(workflow2.Id, workflows[0].Id);
            Assert.AreEqual(workflow1.Id, workflows[1].Id);
            Assert.AreEqual(workflow3.Id, workflows[2].Id);
        }

        #endregion

        #region GetActiveCount Tests

        [Test]
        public void GetActiveCount_NoSuspendedWorkflows_ReturnsCorrectCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }

            long result = store.GetActiveCount();
            Assert.AreEqual(count, result);
        }

        [Test]
        public void GetActiveCount_WithSuspendedWorkflows_ReturnsCorrectCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });

            long result = store.GetActiveCount();
            Assert.AreEqual(count, result);
        }

        #endregion

        #region GetAllByType Tests

        [Test]
        public void GetAllByType_OnExecute_ReturnsCorrectWorkflows()
        {
            // Set up a store with some workflows
            IWorkflowStore store = GetStore();
            store.Save(new BasicWorkflow("Start"));
            store.Save(new SingleInstanceWorkflow("Start"));
            store.Save(new SimpleTwoStateWorkflow("Start"));

            IEnumerable<Workflow> result = store.GetAllByType(typeof(SingleInstanceWorkflow).AssemblyQualifiedName);
            Workflow wf = result.Single();
            Assert.AreEqual(typeof(SingleInstanceWorkflow).FullName, wf.GetType().FullName);
            
        }

        #endregion

        #region GetCompletedCount Tests

        [Test]
        public void GetCompletedCount_OnExecute_ReturnsAccurateCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                Workflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                store.Save(wf);
                store.Archive(wf);
            }

            long result = store.GetCompletedCount();
            Assert.AreEqual(count, result);
        }

        #endregion

        #region GetCompletedCount Tests

        [Test]
        public void GetSuspendedCount_OnExecute_ReturnsAccurateCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            int suspendedCount = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                Workflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                store.Save(wf);
            }
            for (int i = 0; i < suspendedCount; i++)
            {
                Workflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.IsSuspended = true;
                store.Save(wf);
            }

            long result = store.GetSuspendedCount();
            Assert.AreEqual(suspendedCount, result);
        }

        #endregion
    }
}
