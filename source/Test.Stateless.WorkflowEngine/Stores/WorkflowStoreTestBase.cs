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
using Newtonsoft.Json;

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

        protected virtual T DeserializeJsonWorkflow<T>(string json) where T : Workflow
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

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

        #region GetIncomplete Tests

        [Test]
        public void GetIncomplete_WorkflowIsSuspended_IsReturned()
        {
            // factory method for workflows
            Func<bool, BasicWorkflow> createWorkflow = (isSuspended) =>
            {
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
            List<Workflow> workflows = store.GetIncomplete(10).ToList();
            Assert.AreEqual(2, workflows.Count);
            Assert.IsNotNull(workflows.SingleOrDefault(x => x.Id == activeWorkflow.Id));
            Assert.IsNotNull(workflows.SingleOrDefault(x => x.Id == suspendedWorkflow.Id));
        }

        [Test]
        public void GetIncomplete_WorkflowResumeDateInFuture_NotReturned()
        {
            // factory method for workflows
            Func<bool, DateTime, BasicWorkflow> createWorkflow = (isSuspended, resumeOn) =>
            {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.IsSuspended = isSuspended;
                wf.ResumeOn = resumeOn;
                wf.BasicMetaData = Guid.NewGuid().ToString();
                return wf;
            };

            Workflow noResumeDateWorkflow = createWorkflow(false, DateTime.MinValue);
            Workflow resumeDateActiveWorkflow = createWorkflow(true, DateTime.UtcNow.AddMilliseconds(-1));
            Workflow futureDatedWorkflow = createWorkflow(false, DateTime.UtcNow.AddMinutes(3));

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(noResumeDateWorkflow);
            store.Save(resumeDateActiveWorkflow);
            store.Save(futureDatedWorkflow);

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetIncomplete(10).ToList();
            Assert.AreEqual(2, workflows.Count);

            Assert.IsNotNull(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id));
            Assert.IsNotNull(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id));
            Assert.IsNull(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id));
        }

        [Test]
        public void GetIncomplete_MultipleWorkflowsReturned_OrderedByRetryCountBeforeCreateDate()
        {
            // factory method for workflows
            Func<DateTime, int, bool, BasicWorkflow> createWorkflow = (createdOn, retryCount, isSuspended) =>
            {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.CreatedOn = createdOn;
                wf.RetryCount = retryCount;
                wf.IsSuspended = isSuspended;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;

            Workflow workflow1 = createWorkflow(baseDate.AddMinutes(1), 2, true);
            Workflow workflow2 = createWorkflow(baseDate.AddMinutes(-1), 1, false);
            Workflow workflow3 = createWorkflow(baseDate.AddMinutes(1), 3, true);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3 });

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetIncomplete(10).ToList();

            Assert.AreEqual(workflow3.Id, workflows[0].Id);
            Assert.AreEqual(workflow1.Id, workflows[1].Id);
            Assert.AreEqual(workflow2.Id, workflows[2].Id);
        }

        [Test]
        public void GetIncomplete_MultipleWorkflowsReturned_OrderedByCreateDateAfterRetryCount()
        {
            // factory method for workflows
            Func<DateTime, bool, BasicWorkflow> createWorkflow = (createdOn, isSuspended) =>
            {
                BasicWorkflow wf = new BasicWorkflow("Start");
                wf.CreatedOn = createdOn;
                wf.RetryCount = 1;
                wf.IsSuspended = isSuspended;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;

            Workflow workflow1 = createWorkflow(baseDate.AddMinutes(1), true);
            Workflow workflow2 = createWorkflow(baseDate.AddMinutes(2), false);
            Workflow workflow3 = createWorkflow(baseDate.AddMinutes(-1), true);
            Workflow workflow4 = createWorkflow(baseDate.AddMinutes(-2), false);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3, workflow4 });

            // fetch the workflows, only two should be returned
            List<Workflow> workflows = store.GetIncomplete(10).ToList();

            Assert.AreEqual(workflow4.Id, workflows[0].Id);
            Assert.AreEqual(workflow3.Id, workflows[1].Id);
            Assert.AreEqual(workflow1.Id, workflows[2].Id);
            Assert.AreEqual(workflow2.Id, workflows[3].Id);
        }

        #endregion

        #region GetIncompleteCount Tests

        [Test]
        public void GetIncompleteCount_NoSuspendedWorkflows_ReturnsCorrectCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }

            long result = store.GetIncompleteCount();
            Assert.AreEqual(count, result);
        }

        [Test]
        public void GetIncompleteCount_WithSuspendedWorkflows_ReturnsCorrectCount()
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

            long result = store.GetIncompleteCount();
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

        #region GetIncompleteWorkflowsAsJson Tests

        [Test]
        public void GetIncompleteWorkflowsAsJson_WorkflowsFound_SerializesWorkflows()
        {
            Workflow workflow1 = new BasicWorkflow(BasicWorkflow.State.Start);
            Workflow workflow2 = new BasicWorkflow(BasicWorkflow.State.DoingStuff);

            IWorkflowStore store = GetStore();
            store.Save(workflow1);
            store.Save(workflow2);

            // fetch the workflows, only one should be returned
            List<string> documents = store.GetIncompleteWorkflowsAsJson(10).ToList();
            Assert.AreEqual(2, documents.Count);

            // deserialize and check that we have the correct ones
            Workflow w1 = DeserializeJsonWorkflow<BasicWorkflow>(documents[0]);
            Assert.AreEqual(workflow1.Id, w1.Id);
            Workflow w2 = DeserializeJsonWorkflow<BasicWorkflow>(documents[1]);
            Assert.AreEqual(workflow2.Id, w2.Id);

        }

        #endregion

        #region GetWorkflowAsJson Tests

        [Test]
        public void GetWorkflowAsJson_NoSuchDocument_ReturnsNull()
        {
            var store = this.GetStore();

            var doc = store.GetWorkflowAsJson(Guid.NewGuid());
            Assert.IsNull(doc);
        }

        [Test]
        public void GetWorkflowAsJson_DocumentExists_ReturnsDocument()
        {
            IWorkflowStore store = GetStore();
            Workflow wf = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            Guid id = wf.Id;
            store.Save(wf);

            string json = store.GetWorkflowAsJson(id);
            Assert.IsNotNull(json);

            // convert back to the known type and make sure it's ok
            BasicWorkflow workflow = DeserializeJsonWorkflow<BasicWorkflow>(json);
            Assert.IsNotNull(workflow);
            Assert.AreEqual(id, workflow.Id);
            Assert.AreEqual(BasicWorkflow.State.DoingStuff.ToString(), workflow.CurrentState); 
        }

        #endregion

        #region Suspend Tests

        [Test]
        public void Suspend_OnSuspension_UpdatesWorkflowAndSaves()
        {
            IWorkflowStore store = GetStore();

            // setup a new workflow
            BasicWorkflow wf = new BasicWorkflow("Start");
            wf.CreatedOn = DateTime.UtcNow.AddMinutes(-1);
            wf.RetryCount = 0;
            wf.IsSuspended = false;
            store.Save(wf);

            // execute
            store.SuspendWorkflow(wf.Id);

            // assert: fetch the workflow - it should be available and suspended
            Workflow result = store.GetOrDefault(wf.Id);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuspended);
        }

        #endregion

        #region Unsuspend Tests

        [Test]
        public void Ususpend_OnUnsuspension_UpdatesWorkflowAndSaves()
        {
            IWorkflowStore store = GetStore();

            // setup a new workflow
            BasicWorkflow wf = new BasicWorkflow("Start");
            wf.CreatedOn = DateTime.UtcNow.AddMinutes(-1);
            wf.RetryCount = 3;
            wf.ResumeOn = DateTime.UtcNow.AddDays(1);
            wf.IsSuspended = true;
            store.Save(wf);

            // execute
            DateTime beforeSuspend = DateTime.UtcNow;
            store.UnsuspendWorkflow(wf.Id);
            DateTime afterSuspend = DateTime.UtcNow;

            // assert: fetch the workflow - it should be available and unsuspended
            Workflow result = store.GetOrDefault(wf.Id);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuspended);
            Assert.AreEqual(0, result.RetryCount);
            Assert.GreaterOrEqual(result.ResumeOn, beforeSuspend);
            Assert.LessOrEqual(result.ResumeOn, afterSuspend);
        }

        #endregion


    }
}
