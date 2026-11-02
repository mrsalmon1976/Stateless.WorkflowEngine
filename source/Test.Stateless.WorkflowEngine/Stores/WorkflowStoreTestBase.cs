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
using Newtonsoft.Json;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.Counting;
using System.IO;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.Stores
{
    public abstract class WorkflowStoreTestBase
    {
        #region Protected Methods

        protected virtual WorkflowDefinition CreateWorkflowDefinition<T>() where T : Workflow
        {
            Type workflowType = typeof(T);
            WorkflowDefinition workflowDefinition = new WorkflowDefinition();
            workflowDefinition.Name = workflowType.Name;
            workflowDefinition.QualifiedName = workflowType.FullName;
            workflowDefinition.Graph = Path.GetRandomFileName();
            workflowDefinition.LastUpdatedUtc = DateTime.UtcNow;
            return workflowDefinition;
        }

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
            Assert.That(store.GetOrDefault(wf.Id), Is.Null);
            Assert.That(store.GetCompletedOrDefault(wf.Id), Is.Not.Null);
        }

        #endregion

        #region ArchiveAsync Tests

        [Test]
        public async Task ArchiveAsync_WorkflowIsMoved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);

            await store.ArchiveAsync(wf);
            Assert.That(store.GetOrDefault(wf.Id), Is.Null);
            Assert.That(store.GetCompletedOrDefault(wf.Id), Is.Not.Null);
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
            Assert.That(result1, Is.Not.Null);

            store.Delete(workflowId);
            Workflow result2 = store.GetOrDefault(workflowId);
            Assert.That(result2, Is.Null);
        }

        #endregion

        #region DeleteAsync Tests

        [Test]
        public async Task DeleteAsync_OnExecute_RemovesWorkflow()
        {
            Guid workflowId = Guid.NewGuid();
            IWorkflowStore store = GetStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = workflowId;
            store.Save(workflow);

            Workflow result1 = store.GetOrDefault(workflowId);
            Assert.That(result1, Is.Not.Null);

            await store.DeleteAsync(workflowId);
            Workflow result2 = store.GetOrDefault(workflowId);
            Assert.That(result2, Is.Null);
        }

        #endregion

        #region Get Tests

        [Test]
        public void Get_InvalidId_ThrowsException()
        {
            IWorkflowStore store = GetStore();
            Action del = () => store.Get(Guid.NewGuid());
            Assert.Throws<WorkflowNotFoundException>(del);

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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(wf.Id));
        }

        #endregion

        #region GetOrDefaultAsync Tests

        [Test]
        public async Task GetOrDefaultAsync_ExistingId_ReturnsWorkflow()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);

            Workflow result = await store.GetOrDefaultAsync(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(wf.Id));
        }

        [Test]
        public async Task GetOrDefaultAsync_InvalidId_ReturnsNull()
        {
            IWorkflowStore store = GetStore();

            Workflow result = await store.GetOrDefaultAsync(Guid.NewGuid());
            Assert.That(result, Is.Null);
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
            Assert.That(workflows.Count, Is.EqualTo(1));
            Assert.That(workflows[0].Id, Is.EqualTo(activeWorkflow.Id));
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
            Assert.That(workflows.Count, Is.EqualTo(2));

            Assert.That(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id), Is.Null);
        }

        [Test]
        public void GetActive_MultipleWorkflowsReturned_OrderedByPriorityBeforeRetryCount()
        {
            DateTime startTime = DateTime.Now;
            // factory method for workflows
            Func<int, int, BasicWorkflow> createWorkflow = (priority, retryCount) => {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.Priority = priority;
                wf.RetryCount = retryCount;
                wf.CreatedOn = startTime;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;
            
            Workflow workflow1 = createWorkflow(4, 3);
            Workflow workflow2 = createWorkflow(5, 2);
            Workflow workflow3 = createWorkflow(5, 3);
            Workflow workflow4 = createWorkflow(4, 2);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3, workflow4 });

            // fetch the workflows
            List<Workflow> workflows = store.GetActive(10).ToList();

            Assert.That(workflows[0].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow2.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[3].Id, Is.EqualTo(workflow4.Id));
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

            Assert.That(workflows[0].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow2.Id));
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

            Assert.That(workflows[0].Id, Is.EqualTo(workflow2.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow3.Id));
        }

        #endregion

        #region GetActiveAsync Tests

        [Test]
        public async Task GetActiveAsync_WorkflowIsSuspended_NotReturned()
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
            var workflows = await store.GetActiveAsync(10);
            Assert.That(workflows.Count, Is.EqualTo(1));
            Assert.That(workflows.First().Id, Is.EqualTo(activeWorkflow.Id));
        }

        [Test]
        public async Task GetActiveAsync_WorkflowResumeDateInFuture_NotReturned()
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
            var workflows = await store.GetActiveAsync(10);
            Assert.That(workflows.Count, Is.EqualTo(2));

            Assert.That(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id), Is.Null);
        }

        [Test]
        public async Task GetActiveAsync_MultipleWorkflowsReturned_OrderedByPriorityBeforeRetryCount()
        {
            DateTime startTime = DateTime.Now;
            // factory method for workflows
            Func<int, int, BasicWorkflow> createWorkflow = (priority, retryCount) => {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.Priority = priority;
                wf.RetryCount = retryCount;
                wf.CreatedOn = startTime;
                return wf;
            };

            // create the workflows - ensure they are added in an incorrect order
            DateTime baseDate = DateTime.UtcNow;

            Workflow workflow1 = createWorkflow(4, 3);
            Workflow workflow2 = createWorkflow(5, 2);
            Workflow workflow3 = createWorkflow(5, 3);
            Workflow workflow4 = createWorkflow(4, 2);

            // Set up a store with a basic workflow
            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3, workflow4 });

            // fetch the workflows
            var workflows = (await store.GetActiveAsync(10)).ToList();

            Assert.That(workflows[0].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow2.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[3].Id, Is.EqualTo(workflow4.Id));
        }

        [Test]
        public async Task GetActiveAsync_MultipleWorkflowsReturned_OrderedByRetryCountBeforeCreateDate()
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
            var workflows = (await store.GetActiveAsync(10)).ToList();

            Assert.That(workflows[0].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow2.Id));
        }

        [Test]
        public async Task GetActiveAsync_MultipleWorkflowsReturned_OrderedByCreateDateAfterRetryCount()
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
            var workflows = (await store.GetActiveAsync(10)).ToList();

            Assert.That(workflows[0].Id, Is.EqualTo(workflow2.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow3.Id));
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
            Assert.That(result, Is.EqualTo(count));
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
            Assert.That(result, Is.EqualTo(count));
        }

        #endregion

        #region GetActiveCountAsync Tests

        [Test]
        public async Task GetActiveCountAsync_NoSuspendedWorkflows_ReturnsCorrectCount()
        {
            // Set up a store with some basic workflows
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }

            long result = await store.GetActiveCountAsync();
            Assert.That(result, Is.EqualTo(count));
        }

        [Test]
        public async Task GetActiveCountAsync_WithSuspendedWorkflows_ReturnsCorrectCount()
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

            long result = await store.GetActiveCountAsync();
            Assert.That(result, Is.EqualTo(count));
        }

        #endregion

        #region GetDefinitions Tests

        [Test]
        public void GetDefinitions_NoDefinitions_EmptyEnumerableReturned()
        {
            IWorkflowStore store = GetStore();

            IEnumerable<WorkflowDefinition> savedDefinitions = store.GetDefinitions();

            Assert.That(savedDefinitions.Count(), Is.EqualTo(0));
        }

        [Test]
        public void GetDefinitions_DefinitionsAdded_AllDefinitionsReturned()
        {
            IWorkflowStore store = GetStore();
            WorkflowDefinition basicWorkflowDefinition = this.CreateWorkflowDefinition<BasicWorkflow>();
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            store.SaveDefinition(basicWorkflowDefinition);
            store.SaveDefinition(brokenWorkflowDefinition);

            IEnumerable<WorkflowDefinition> savedDefinitions = store.GetDefinitions();

            Assert.That(savedDefinitions.Count(), Is.EqualTo(2));
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == basicWorkflowDefinition.QualifiedName), Is.Not.Null);
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == brokenWorkflowDefinition.QualifiedName), Is.Not.Null);
        }

        #endregion

        #region GetDefinitionByQualifiedName Tests

        [Test]
        public void GetDefinitionByQualifiedName_NotFound_ReturnsNull()
        {
            IWorkflowStore store = GetStore();
            Type basicWorkflowType = typeof(BasicWorkflow);
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            store.SaveDefinition(brokenWorkflowDefinition);

            WorkflowDefinition result = store.GetDefinitionByQualifiedName(basicWorkflowType.FullName);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetDefinitionByQualifiedName_Exists_Returned()
        {
            IWorkflowStore store = GetStore();
            Type basicWorkflowType = typeof(BasicWorkflow);
            WorkflowDefinition basicWorkflowDefinition = this.CreateWorkflowDefinition<BasicWorkflow>();
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            store.SaveDefinition(basicWorkflowDefinition);
            store.SaveDefinition(brokenWorkflowDefinition);

            WorkflowDefinition result = store.GetDefinitionByQualifiedName(basicWorkflowType.FullName);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo(basicWorkflowType.Name));
            Assert.That(result.QualifiedName, Is.EqualTo(basicWorkflowType.FullName));
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
            Assert.That(workflows.Count, Is.EqualTo(2));
            Assert.That(workflows.SingleOrDefault(x => x.Id == activeWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.SingleOrDefault(x => x.Id == suspendedWorkflow.Id), Is.Not.Null);
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
            Assert.That(workflows.Count, Is.EqualTo(2));

            Assert.That(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id), Is.Null);
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

            Assert.That(workflows[0].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow2.Id));
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

            Assert.That(workflows[0].Id, Is.EqualTo(workflow4.Id));
            Assert.That(workflows[1].Id, Is.EqualTo(workflow3.Id));
            Assert.That(workflows[2].Id, Is.EqualTo(workflow1.Id));
            Assert.That(workflows[3].Id, Is.EqualTo(workflow2.Id));
        }

        #endregion

        #region GetIncompleteAsync Tests

        [Test]
        public async Task GetIncompleteAsync_WorkflowIsSuspended_IsReturned()
        {
            Func<bool, BasicWorkflow> createWorkflow = (isSuspended) =>
            {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.IsSuspended = isSuspended;
                return wf;
            };

            Workflow activeWorkflow = createWorkflow(false);
            Workflow suspendedWorkflow = createWorkflow(true);

            IWorkflowStore store = GetStore();
            store.Save(activeWorkflow);
            store.Save(suspendedWorkflow);

            var workflows = (await store.GetIncompleteAsync(10)).ToList();
            Assert.That(workflows.Count, Is.EqualTo(2));
            Assert.That(workflows.SingleOrDefault(x => x.Id == activeWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.SingleOrDefault(x => x.Id == suspendedWorkflow.Id), Is.Not.Null);
        }

        [Test]
        public async Task GetIncompleteAsync_WorkflowResumeDateInFuture_NotReturned()
        {
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

            IWorkflowStore store = GetStore();
            store.Save(noResumeDateWorkflow);
            store.Save(resumeDateActiveWorkflow);
            store.Save(futureDatedWorkflow);

            var workflows = (await store.GetIncompleteAsync(10)).ToList();
            Assert.That(workflows.Count, Is.EqualTo(2));
            Assert.That(workflows.FirstOrDefault(x => x.Id == noResumeDateWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == resumeDateActiveWorkflow.Id), Is.Not.Null);
            Assert.That(workflows.FirstOrDefault(x => x.Id == futureDatedWorkflow.Id), Is.Null);
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
            Assert.That(result, Is.EqualTo(count));
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
            Assert.That(result, Is.EqualTo(count + 3));
        }

		#endregion

        #region GetIncompleteCountAsync Tests

        [Test]
        public async Task GetIncompleteCountAsync_NoSuspendedWorkflows_ReturnsCorrectCount()
        {
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }

            long result = await store.GetIncompleteCountAsync();
            Assert.That(result, Is.EqualTo(count));
        }

        [Test]
        public async Task GetIncompleteCountAsync_WithSuspendedWorkflows_ReturnsCorrectCount()
        {
            IWorkflowStore store = GetStore();
            int count = new Random().Next(2, 10);
            for (int i = 0; i < count; i++)
            {
                store.Save(new BasicWorkflow(BasicWorkflow.State.Start));
            }
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });
            store.Save(new BasicWorkflow(BasicWorkflow.State.Start) { IsSuspended = true });

            long result = await store.GetIncompleteCountAsync();
            Assert.That(result, Is.EqualTo(count + 3));
        }

        #endregion

		#region GetAllByQualifiedName Tests

		[Test]
        public void GetAllByQualifiedName_OnExecute_ReturnsCorrectWorkflows()
        {
            // Set up a store with some workflows
            IWorkflowStore store = GetStore();
            store.Save(new BasicWorkflow("Start"));
            store.Save(new SingleInstanceWorkflow("Start"));
            store.Save(new SimpleTwoStateWorkflow("Start"));

            IEnumerable<Workflow> result = store.GetAllByQualifiedName(typeof(SingleInstanceWorkflow).FullName);
            Workflow wf = result.Single();
            Assert.That(wf.GetType().FullName, Is.EqualTo(typeof(SingleInstanceWorkflow).FullName));
            
        }


		#endregion

		#region GetAllByQualifiedNameAsync Tests

		[Test]
        public async Task GetAllByQualifiedNameAsync_OnExecute_ReturnsCorrectWorkflows()
        {
            // Set up a store with some workflows
            IWorkflowStore store = GetStore();
            store.Save(new BasicWorkflow("Start"));
            store.Save(new SingleInstanceWorkflow("Start"));
            store.Save(new SimpleTwoStateWorkflow("Start"));

            IEnumerable<Workflow> result = await store.GetAllByQualifiedNameAsync(typeof(SingleInstanceWorkflow).FullName);
            Workflow wf = result.Single();
            Assert.That(wf.GetType().FullName, Is.EqualTo(typeof(SingleInstanceWorkflow).FullName));
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
			Assert.That(wf.GetType().AssemblyQualifiedName, Is.EqualTo(typeof(SingleInstanceWorkflow).AssemblyQualifiedName));

		}


		#endregion

		#region GetAllByTypeAsync Tests

		[Test]
        public async Task GetAllByTypeAsync_OnExecute_ReturnsCorrectWorkflows()
        {
            // Set up a store with some workflows
            IWorkflowStore store = GetStore();
            store.Save(new BasicWorkflow("Start"));
            store.Save(new SingleInstanceWorkflow("Start"));
            store.Save(new SimpleTwoStateWorkflow("Start"));

            IEnumerable<Workflow> result = await store.GetAllByTypeAsync(typeof(SingleInstanceWorkflow).AssemblyQualifiedName);
            Workflow wf = result.Single();
            Assert.That(wf.GetType().AssemblyQualifiedName, Is.EqualTo(typeof(SingleInstanceWorkflow).AssemblyQualifiedName));
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
            Assert.That(result, Is.EqualTo(count));
        }

        #endregion

        #region GetCompletedCountAsync Tests

        [Test]
        public async Task GetCompletedCountAsync_OnExecute_ReturnsAccurateCount()
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

            long result = await store.GetCompletedCountAsync();
            Assert.That(result, Is.EqualTo(count));
        }

        #endregion

        #region GetCompletedOrDefaultAsync Tests

        [Test]
        public async Task GetCompletedOrDefaultAsync_ExistingId_ReturnsWorkflow()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);
            store.Archive(wf);

            Workflow result = await store.GetCompletedOrDefaultAsync(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(wf.Id));
        }

        [Test]
        public async Task GetCompletedOrDefaultAsync_InvalidId_ReturnsNull()
        {
            IWorkflowStore store = GetStore();

            Workflow result = await store.GetCompletedOrDefaultAsync(Guid.NewGuid());
            Assert.That(result, Is.Null);
        }

        #endregion

        #region GetSuspendedCount Tests

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
            Assert.That(result, Is.EqualTo(suspendedCount));
        }

        #endregion

        #region GetSuspendedCountAsync Tests

        [Test]
        public async Task GetSuspendedCountAsync_OnExecute_ReturnsAccurateCount()
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

            long result = await store.GetSuspendedCountAsync();
            Assert.That(result, Is.EqualTo(suspendedCount));
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
            Assert.That(documents.Count, Is.EqualTo(2));

            // deserialize and check that we have the correct ones
            Workflow w1 = DeserializeJsonWorkflow<BasicWorkflow>(documents[0]);
            Assert.That(w1.Id, Is.EqualTo(workflow1.Id));
            Workflow w2 = DeserializeJsonWorkflow<BasicWorkflow>(documents[1]);
            Assert.That(w2.Id, Is.EqualTo(workflow2.Id));

        }

        #endregion

        #region GetWorkflowAsJson Tests

        [Test]
        public void GetWorkflowAsJson_NoSuchDocument_ReturnsNull()
        {
            var store = this.GetStore();

            var doc = store.GetWorkflowAsJson(Guid.NewGuid());
            Assert.That(doc, Is.Null);
        }

        [Test]
        public void GetWorkflowAsJson_DocumentExists_ReturnsDocument()
        {
            IWorkflowStore store = GetStore();
            Workflow wf = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            Guid id = wf.Id;
            store.Save(wf);

            string json = store.GetWorkflowAsJson(id);
            Assert.That(json, Is.Not.Null);

            // convert back to the known type and make sure it's ok
            BasicWorkflow workflow = DeserializeJsonWorkflow<BasicWorkflow>(json);
            Assert.That(workflow, Is.Not.Null);
            Assert.That(workflow.Id, Is.EqualTo(id));
            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.DoingStuff.ToString())); 
        }

        #endregion

        #region Save Tests

        [Test]
        public void Save_NewWorkflow_WorkflowIsSaved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            store.Save(wf);

            Workflow result = store.GetOrDefault(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(wf.Id));
        }

        [Test]
        public void Save_ExistingWorkflow_WorkflowIsUpdated()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            wf.BasicMetaData = "Initial";
            store.Save(wf);

            wf.BasicMetaData = "Updated";
            wf.RetryCount = 3;
            store.Save(wf);

            BasicWorkflow result = store.Get<BasicWorkflow>(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.RetryCount, Is.EqualTo(3));
        }

        [Test]
        public void Save_MultipleWorkflows_AllWorkflowsSaved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf1 = new BasicWorkflow(BasicWorkflow.State.Start);
            BasicWorkflow wf2 = new BasicWorkflow(BasicWorkflow.State.Start);
            BasicWorkflow wf3 = new BasicWorkflow(BasicWorkflow.State.Start);

            store.Save(new[] { wf1, wf2, wf3 });

            Assert.That(store.GetOrDefault(wf1.Id), Is.Not.Null);
            Assert.That(store.GetOrDefault(wf2.Id), Is.Not.Null);
            Assert.That(store.GetOrDefault(wf3.Id), Is.Not.Null);
        }

        #endregion

        #region SaveAsync Tests

        [Test]
        public async Task SaveAsync_NewWorkflow_WorkflowIsSaved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            await store.SaveAsync(wf);

            Workflow result = store.GetOrDefault(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(wf.Id));
        }

        [Test]
        public async Task SaveAsync_ExistingWorkflow_WorkflowIsUpdated()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
            wf.BasicMetaData = "Initial";
            await store.SaveAsync(wf);

            wf.BasicMetaData = "Updated";
            wf.RetryCount = 3;
            await store.SaveAsync(wf);

            BasicWorkflow result = store.Get<BasicWorkflow>(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.RetryCount, Is.EqualTo(3));
        }

        [Test]
        public async Task SaveAsync_MultipleWorkflows_AllWorkflowsSaved()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow wf1 = new BasicWorkflow(BasicWorkflow.State.Start);
            BasicWorkflow wf2 = new BasicWorkflow(BasicWorkflow.State.Start);
            BasicWorkflow wf3 = new BasicWorkflow(BasicWorkflow.State.Start);

            await store.SaveAsync(new[] { wf1, wf2, wf3 });

            Assert.That(store.GetOrDefault(wf1.Id), Is.Not.Null);
            Assert.That(store.GetOrDefault(wf2.Id), Is.Not.Null);
            Assert.That(store.GetOrDefault(wf3.Id), Is.Not.Null);
        }

        #endregion

        #region SaveDefinitionAsync Tests

        [Test]
        public async Task SaveDefinitionAsync_NewDefinitions_DefinitionsAdded()
        {
            IWorkflowStore store = GetStore();
            WorkflowDefinition basicWorkflowDefinition = this.CreateWorkflowDefinition<BasicWorkflow>();
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            await store.SaveDefinitionAsync(basicWorkflowDefinition);
            await store.SaveDefinitionAsync(brokenWorkflowDefinition);

            IEnumerable<WorkflowDefinition> savedDefinitions = store.GetDefinitions();

            Assert.That(savedDefinitions.Count(), Is.EqualTo(2));
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == basicWorkflowDefinition.QualifiedName), Is.Not.Null);
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == brokenWorkflowDefinition.QualifiedName), Is.Not.Null);
        }

        #endregion

        #region SaveDefinition Tests

        [Test]
        public void SaveDefinition_NewDefinitions_DefinitionAdded()
        {
            IWorkflowStore store = GetStore();
            WorkflowDefinition basicWorkflowDefinition = this.CreateWorkflowDefinition<BasicWorkflow>();
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            store.SaveDefinition(basicWorkflowDefinition);
            store.SaveDefinition(brokenWorkflowDefinition);

            IEnumerable<WorkflowDefinition> savedDefinitions = store.GetDefinitions();

            Assert.That(savedDefinitions.Count(), Is.EqualTo(2));
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == basicWorkflowDefinition.QualifiedName), Is.Not.Null);
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == brokenWorkflowDefinition.QualifiedName), Is.Not.Null);
        }

        [Test]
        public void SaveDefinition_DefinitionExists_DefinitionUpdated()
        {
            IWorkflowStore store = GetStore();
            WorkflowDefinition basicWorkflowDefinition1 = this.CreateWorkflowDefinition<BasicWorkflow>();
            WorkflowDefinition basicWorkflowDefinition2 = this.CreateWorkflowDefinition<BasicWorkflow>();
            basicWorkflowDefinition2.Id = basicWorkflowDefinition1.Id;
            WorkflowDefinition brokenWorkflowDefinition = this.CreateWorkflowDefinition<BrokenWorkflow>();

            store.SaveDefinition(basicWorkflowDefinition1);
            store.SaveDefinition(basicWorkflowDefinition2);
            store.SaveDefinition(brokenWorkflowDefinition);

            IEnumerable<WorkflowDefinition> savedDefinitions = store.GetDefinitions();

            Assert.That(savedDefinitions.Count(), Is.EqualTo(2));
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == basicWorkflowDefinition1.QualifiedName), Is.Not.Null);
            Assert.That(savedDefinitions.SingleOrDefault(x => x.QualifiedName == brokenWorkflowDefinition.QualifiedName), Is.Not.Null);
        }

        #endregion

        #region Suspend Tests

        [Test]
        public void Suspend_WorkflowNotFound_ThrowsWorkflowNotFoundException()
        {
            IWorkflowStore store = GetStore();
            Assert.Throws<WorkflowNotFoundException>(() => store.SuspendWorkflow(Guid.NewGuid()));
        }

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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuspended, Is.True);
        }

        #endregion

        #region SuspendAsync Tests

        [Test]
        public void SuspendAsync_WorkflowNotFound_ThrowsWorkflowNotFoundException()
        {
            IWorkflowStore store = GetStore();
            Assert.ThrowsAsync<WorkflowNotFoundException>(() => store.SuspendWorkflowAsync(Guid.NewGuid()));
        }

        [Test]
        public async Task SuspendAsync_OnSuspension_UpdatesWorkflowAndSaves()
        {
            IWorkflowStore store = GetStore();

            // setup a new workflow
            BasicWorkflow wf = new BasicWorkflow("Start");
            wf.CreatedOn = DateTime.UtcNow.AddMinutes(-1);
            wf.RetryCount = 0;
            wf.IsSuspended = false;
            store.Save(wf);

            // execute
            await store.SuspendWorkflowAsync(wf.Id);

            // assert: fetch the workflow - it should be available and suspended
            Workflow result = store.GetOrDefault(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuspended, Is.True);
        }

        #endregion

        #region Unsuspend Tests

        [Test]
        public void Unsuspend_WorkflowNotFound_ThrowsWorkflowNotFoundException()
        {
            IWorkflowStore store = GetStore();
            Assert.Throws<WorkflowNotFoundException>(() => store.UnsuspendWorkflow(Guid.NewGuid()));
        }

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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuspended, Is.False);
            Assert.That(result.RetryCount, Is.EqualTo(0));
            Assert.That(result.ResumeOn, Is.GreaterThan(beforeSuspend.AddMilliseconds(-1)));
			Assert.That(result.ResumeOn, Is.LessThan(afterSuspend.AddMilliseconds(1)));
        }

        #endregion

        #region UnsuspendAsync Tests

        [Test]
        public void UnsuspendAsync_WorkflowNotFound_ThrowsWorkflowNotFoundException()
        {
            IWorkflowStore store = GetStore();
            Assert.ThrowsAsync<WorkflowNotFoundException>(() => store.UnsuspendWorkflowAsync(Guid.NewGuid()));
        }

        [Test]
        public async Task UnsuspendAsync_OnUnsuspension_UpdatesWorkflowAndSaves()
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
            DateTime beforeUnsuspend = DateTime.UtcNow;
            await store.UnsuspendWorkflowAsync(wf.Id);
            DateTime afterUnsuspend = DateTime.UtcNow;

            // assert: fetch the workflow - it should be available and unsuspended
            Workflow result = store.GetOrDefault(wf.Id);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuspended, Is.False);
            Assert.That(result.RetryCount, Is.EqualTo(0));
            Assert.That(result.ResumeOn, Is.GreaterThan(beforeUnsuspend.AddMilliseconds(-1)));
            Assert.That(result.ResumeOn, Is.LessThan(afterUnsuspend.AddMilliseconds(1)));
        }

        #endregion

        #region ClaimNext Tests

        [Test]
        public void ClaimNext_NoWorkflows_ReturnsNull()
        {
            IWorkflowStore store = GetStore();
            Assert.That(store.ClaimNext("node1"), Is.Null);
        }

        [Test]
        public void ClaimNext_WorkflowAvailable_LocksAndReturnsWorkflow()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow workflow = CreateClaimableWorkflow();
            store.Save(workflow);

            DateTime beforeClaim = DateTime.UtcNow;
            Workflow claimed = store.ClaimNext("node1");

            Assert.That(claimed.Id, Is.EqualTo(workflow.Id));
            Assert.That(claimed.LockedBy, Is.EqualTo("node1"));
            Assert.That(claimed.LockedOn, Is.GreaterThan(beforeClaim.AddSeconds(-1)));
            Assert.That(store.GetOrDefault(workflow.Id).LockedBy, Is.EqualTo("node1"));
            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node1" }));
        }

        [Test]
        public void ClaimNext_WorkflowAlreadyClaimed_NotReturned()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());

            Assert.That(store.ClaimNext("node1"), Is.Not.Null);
            Assert.That(store.ClaimNext("node2"), Is.Null);
        }

        [Test]
        public void ClaimNext_SuspendedAndFutureWorkflows_NotReturned()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow suspended = CreateClaimableWorkflow();
            suspended.IsSuspended = true;
            BasicWorkflow future = CreateClaimableWorkflow();
            future.ResumeOn = DateTime.UtcNow.AddMinutes(5);
            BasicWorkflow available = CreateClaimableWorkflow();
            store.Save(new[] { suspended, future, available });

            Assert.That(store.ClaimNext("node1").Id, Is.EqualTo(available.Id));
            Assert.That(store.ClaimNext("node1"), Is.Null);
        }

        [Test]
        public void ClaimNext_MultipleWorkflows_ClaimedByPriorityThenRetryCountThenCreatedOn()
        {
            DateTime baseDate = DateTime.UtcNow.AddMinutes(-10);
            Func<int, int, int, BasicWorkflow> createWorkflow = (priority, retryCount, createdMinutes) =>
            {
                BasicWorkflow wf = CreateClaimableWorkflow();
                wf.Priority = priority;
                wf.RetryCount = retryCount;
                wf.CreatedOn = baseDate.AddMinutes(createdMinutes);
                return wf;
            };

            BasicWorkflow workflow1 = createWorkflow(1, 0, 1);
            BasicWorkflow workflow2 = createWorkflow(2, 0, 2);
            BasicWorkflow workflow3 = createWorkflow(2, 1, 3);
            BasicWorkflow workflow4 = createWorkflow(1, 0, 0);

            IWorkflowStore store = GetStore();
            store.Save(new[] { workflow1, workflow2, workflow3, workflow4 });

            Guid[] claimOrder = Enumerable.Range(0, 4).Select(x => store.ClaimNext("node1").Id).ToArray();
            Assert.That(claimOrder, Is.EqualTo(new[] { workflow3.Id, workflow2.Id, workflow4.Id, workflow1.Id }));
        }

        [Test]
        public void ClaimNext_ConcurrentNodes_EachWorkflowClaimedOnce()
        {
            const int WorkflowCount = 40;
            IWorkflowStore store = GetStore();
            store.Save(Enumerable.Range(0, WorkflowCount).Select(x => CreateClaimableWorkflow()).ToList());

            System.Collections.Concurrent.ConcurrentBag<Guid> claimedIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();
            Parallel.For(0, 4, nodeIndex =>
            {
                Workflow claimed;
                while ((claimed = store.ClaimNext("node" + nodeIndex)) != null)
                {
                    claimedIds.Add(claimed.Id);
                }
            });

            Assert.That(claimedIds.Count, Is.EqualTo(WorkflowCount));
            Assert.That(claimedIds.Distinct().Count(), Is.EqualTo(WorkflowCount));
        }

        [Test]
        public async Task ClaimNextAsync_WorkflowAvailable_LocksAndReturnsWorkflow()
        {
            IWorkflowStore store = GetStore();
            BasicWorkflow workflow = CreateClaimableWorkflow();
            store.Save(workflow);

            Workflow claimed = await store.ClaimNextAsync("node1");

            Assert.That(claimed.Id, Is.EqualTo(workflow.Id));
            Assert.That(claimed.LockedBy, Is.EqualTo("node1"));
            Assert.That(await store.ClaimNextAsync("node2"), Is.Null);
        }

        #endregion

        #region SaveClaimed Tests

        [Test]
        public void SaveClaimed_LockedByNode_SavesAndReleasesLock()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            BasicWorkflow claimed = (BasicWorkflow)store.ClaimNext("node1");

            claimed.BasicMetaData = "updated";
            claimed.LockedBy = null;
            claimed.LockedOn = null;
            bool result = store.SaveClaimed(claimed, "node1");

            Assert.That(result, Is.True);
            BasicWorkflow saved = store.Get<BasicWorkflow>(claimed.Id);
            Assert.That(saved.BasicMetaData, Is.EqualTo("updated"));
            Assert.That(saved.LockedBy, Is.Null);
            Assert.That(saved.LockedOn, Is.Null);
            Assert.That(store.GetLockOwners(), Is.Empty);
            Assert.That(store.ClaimNext("node2"), Is.Not.Null);
        }

        [Test]
        public void SaveClaimed_LockKept_WorkflowStaysLocked()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");

            Assert.That(store.SaveClaimed(claimed, "node1"), Is.True);

            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node1" }));
            Assert.That(store.ClaimNext("node2"), Is.Null);
        }

        [Test]
        public void SaveClaimed_LockReleasedButNotReclaimed_Saves()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            BasicWorkflow claimed = (BasicWorkflow)store.ClaimNext("node1");
            store.ReleaseLocks("node1");

            claimed.BasicMetaData = "updated";
            claimed.LockedBy = null;
            Assert.That(store.SaveClaimed(claimed, "node1"), Is.True);
            Assert.That(store.Get<BasicWorkflow>(claimed.Id).BasicMetaData, Is.EqualTo("updated"));
        }

        [Test]
        public void SaveClaimed_LockedByAnotherNode_NotSaved()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");
            store.ReleaseLocks("node1");
            store.ClaimNext("node2");

            Assert.That(store.SaveClaimed(claimed, "node1"), Is.False);
            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node2" }));
        }

        [Test]
        public void SaveClaimed_WorkflowDeleted_ReturnsFalse()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");
            store.Delete(claimed.Id);

            Assert.That(store.SaveClaimed(claimed, "node1"), Is.False);
            Assert.That(store.GetOrDefault(claimed.Id), Is.Null);
        }

        [Test]
        public async Task SaveClaimedAsync_LockedByNode_Saves()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            BasicWorkflow claimed = (BasicWorkflow)await store.ClaimNextAsync("node1");

            claimed.BasicMetaData = "updated";
            claimed.LockedBy = null;
            Assert.That(await store.SaveClaimedAsync(claimed, "node1"), Is.True);
            Assert.That(store.Get<BasicWorkflow>(claimed.Id).BasicMetaData, Is.EqualTo("updated"));
            Assert.That(store.GetLockOwners(), Is.Empty);
        }

        [Test]
        public async Task SaveClaimedAsync_LockedByAnotherNode_NotSaved()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = await store.ClaimNextAsync("node1");
            await store.ReleaseLocksAsync("node1");
            await store.ClaimNextAsync("node2");

            Assert.That(await store.SaveClaimedAsync(claimed, "node1"), Is.False);
            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node2" }));
        }

        #endregion

        #region ArchiveClaimed Tests

        [Test]
        public void ArchiveClaimed_LockedByNode_Archived()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");
            claimed.LockedBy = null;

            Assert.That(store.ArchiveClaimed(claimed, "node1"), Is.True);
            Assert.That(store.GetOrDefault(claimed.Id), Is.Null);
            Assert.That(store.GetCompletedOrDefault(claimed.Id), Is.Not.Null);
            Assert.That(store.GetLockOwners(), Is.Empty);
        }

        [Test]
        public void ArchiveClaimed_LockedByAnotherNode_NotArchived()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");
            store.ReleaseLocks("node1");
            store.ClaimNext("node2");

            Assert.That(store.ArchiveClaimed(claimed, "node1"), Is.False);
            Assert.That(store.GetOrDefault(claimed.Id), Is.Not.Null);
            Assert.That(store.GetCompletedOrDefault(claimed.Id), Is.Null);
        }

        [Test]
        public async Task ArchiveClaimedAsync_LockedByNode_Archived()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = await store.ClaimNextAsync("node1");

            Assert.That(await store.ArchiveClaimedAsync(claimed, "node1"), Is.True);
            Assert.That(store.GetOrDefault(claimed.Id), Is.Null);
            Assert.That(store.GetCompletedOrDefault(claimed.Id), Is.Not.Null);
        }

        [Test]
        public async Task ArchiveClaimedAsync_LockedByAnotherNode_NotArchived()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = await store.ClaimNextAsync("node1");
            await store.ReleaseLocksAsync("node1");
            await store.ClaimNextAsync("node2");

            Assert.That(await store.ArchiveClaimedAsync(claimed, "node1"), Is.False);
            Assert.That(store.GetOrDefault(claimed.Id), Is.Not.Null);
        }

        #endregion

        #region ReleaseLocks Tests

        [Test]
        public void ReleaseLocks_MultipleNodes_OnlyReleasesNodesLocks()
        {
            IWorkflowStore store = GetStore();
            store.Save(Enumerable.Range(0, 3).Select(x => CreateClaimableWorkflow()).ToList());
            store.ClaimNext("node1");
            store.ClaimNext("node1");
            store.ClaimNext("node2");

            store.ReleaseLocks("node1");

            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node2" }));
            Assert.That(store.ClaimNext("node3"), Is.Not.Null);
            Assert.That(store.ClaimNext("node3"), Is.Not.Null);
            Assert.That(store.ClaimNext("node3"), Is.Null);
        }

        [Test]
        public async Task ReleaseLocksAsync_NodeHasLocks_LocksReleased()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            Workflow claimed = store.ClaimNext("node1");

            await store.ReleaseLocksAsync("node1");

            Assert.That(store.GetLockOwners(), Is.Empty);
            Workflow released = store.GetOrDefault(claimed.Id);
            Assert.That(released.LockedBy, Is.Null);
            Assert.That(released.LockedOn, Is.Null);
        }

        [Test]
        public void ReleaseStaleLocks_OnlyOlderLocksNotExcludedAreReleased()
        {
            IWorkflowStore store = GetStore();
            store.Save(Enumerable.Range(0, 3).Select(x => CreateClaimableWorkflow()).ToList());
            Workflow claimed1 = store.ClaimNext("node1");
            Workflow claimed2 = store.ClaimNext("node1");
            Workflow claimed3 = store.ClaimNext("node1");

            // the locks were taken after this time, so nothing is released
            store.ReleaseStaleLocks("node1", DateTime.UtcNow.AddMinutes(-1), new Guid[0]);
            Assert.That(store.ClaimNext("node2"), Is.Null);

            store.ReleaseStaleLocks("node1", DateTime.UtcNow.AddMinutes(1), new[] { claimed2.Id });
            Assert.That(store.GetOrDefault(claimed1.Id).LockedBy, Is.Null);
            Assert.That(store.GetOrDefault(claimed2.Id).LockedBy, Is.EqualTo("node1"));
            Assert.That(store.GetOrDefault(claimed3.Id).LockedBy, Is.Null);
        }

        [Test]
        public async Task ReleaseStaleLocksAsync_OnlyOlderLocksNotExcludedAreReleased()
        {
            IWorkflowStore store = GetStore();
            store.Save(Enumerable.Range(0, 2).Select(x => CreateClaimableWorkflow()).ToList());
            Workflow claimed1 = store.ClaimNext("node1");
            Workflow claimed2 = store.ClaimNext("node1");

            await store.ReleaseStaleLocksAsync("node1", DateTime.UtcNow.AddMinutes(1), new[] { claimed2.Id });

            Assert.That(store.GetOrDefault(claimed1.Id).LockedBy, Is.Null);
            Assert.That(store.GetOrDefault(claimed2.Id).LockedBy, Is.EqualTo("node1"));
        }

        [Test]
        public void ReleaseStaleLocks_OtherNodesLocks_NotReleased()
        {
            IWorkflowStore store = GetStore();
            store.Save(CreateClaimableWorkflow());
            store.ClaimNext("node2");

            store.ReleaseStaleLocks("node1", DateTime.UtcNow.AddMinutes(1), new Guid[0]);

            Assert.That(store.GetLockOwners(), Is.EqualTo(new[] { "node2" }));
        }

        [Test]
        public async Task GetLockOwnersAsync_LocksHeld_ReturnsDistinctOwners()
        {
            IWorkflowStore store = GetStore();
            Assert.That(await store.GetLockOwnersAsync(), Is.Empty);

            store.Save(Enumerable.Range(0, 3).Select(x => CreateClaimableWorkflow()).ToList());
            store.ClaimNext("node1");
            store.ClaimNext("node1");
            store.ClaimNext("node2");

            Assert.That((await store.GetLockOwnersAsync()).OrderBy(x => x), Is.EqualTo(new[] { "node1", "node2" }));
        }

        #endregion

        #region Node Tests

        [Test]
        public void RegisterNode_NewNode_NodeReturnedByGetNodes()
        {
            IWorkflowStore store = GetStore();
            DateTime beforeRegister = DateTime.UtcNow.AddSeconds(-5);

            store.RegisterNode(new WorkflowNode() { Id = "node1", Name = "Server One", StartedOn = DateTime.UtcNow });

            WorkflowNode node = store.GetNodes().Single();
            Assert.That(node.Id, Is.EqualTo("node1"));
            Assert.That(node.Name, Is.EqualTo("Server One"));
            Assert.That(node.LastHeartbeat, Is.GreaterThan(beforeRegister));
        }

        [Test]
        public async Task RegisterNodeAsync_NewNode_NodeReturnedByGetNodesAsync()
        {
            IWorkflowStore store = GetStore();
            await store.RegisterNodeAsync(new WorkflowNode() { Id = "node1", Name = "Server One", StartedOn = DateTime.UtcNow });
            await store.RegisterNodeAsync(new WorkflowNode() { Id = "node2", Name = "Server Two", StartedOn = DateTime.UtcNow });

            Assert.That((await store.GetNodesAsync()).Select(x => x.Id).OrderBy(x => x), Is.EqualTo(new[] { "node1", "node2" }));
        }

        [Test]
        public async Task Heartbeat_NodeExists_ReturnsTrueAndUpdatesHeartbeat()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });
            DateTime registeredHeartbeat = store.GetNodes().Single().LastHeartbeat;
            await Task.Delay(50);

            Assert.That(store.Heartbeat("node1"), Is.True);
            Assert.That(store.GetNodes().Single().LastHeartbeat, Is.GreaterThan(registeredHeartbeat));
            Assert.That(await store.HeartbeatAsync("node1"), Is.True);
        }

        [Test]
        public async Task Heartbeat_NodeRemoved_ReturnsFalseAndDoesNotRecreateNode()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });
            store.RemoveNode("node1");

            Assert.That(store.Heartbeat("node1"), Is.False);
            Assert.That(await store.HeartbeatAsync("node1"), Is.False);
            Assert.That(store.GetNodes(), Is.Empty);
        }

        [Test]
        public async Task RemoveNodeAsync_NodeExists_NodeRemoved()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });
            store.RegisterNode(new WorkflowNode() { Id = "node2", StartedOn = DateTime.UtcNow });

            await store.RemoveNodeAsync("node1");

            Assert.That(store.GetNodes().Select(x => x.Id), Is.EqualTo(new[] { "node2" }));
        }

        [Test]
        public async Task RemoveStaleNodes_RecentHeartbeat_NotRemoved()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });

            Assert.That(store.RemoveStaleNodes(TimeSpan.FromMinutes(1)), Is.EqualTo(0));
            Assert.That(await store.RemoveStaleNodesAsync(TimeSpan.FromMinutes(1)), Is.EqualTo(0));
            Assert.That(store.GetNodes().Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task RemoveStaleNodes_HeartbeatOlderThanTimeout_Removed()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });
            await Task.Delay(1500);
            store.RegisterNode(new WorkflowNode() { Id = "node2", StartedOn = DateTime.UtcNow });

            Assert.That(store.RemoveStaleNodes(TimeSpan.FromSeconds(1)), Is.EqualTo(1));
            Assert.That(store.GetNodes().Select(x => x.Id), Is.EqualTo(new[] { "node2" }));
        }

        [Test]
        public async Task RemoveStaleNodesAsync_HeartbeatOlderThanTimeout_Removed()
        {
            IWorkflowStore store = GetStore();
            store.RegisterNode(new WorkflowNode() { Id = "node1", StartedOn = DateTime.UtcNow });
            await Task.Delay(1500);

            Assert.That(await store.RemoveStaleNodesAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(1));
            Assert.That(store.GetNodes(), Is.Empty);
        }

        #endregion

        #region Multiple Server Tests

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_MultipleServersSharingStore_EachStepExecutedOnce(bool isAsync)
        {
            const int WorkflowCount = 100;
            IWorkflowStore store = GetStore();
            List<Guid> workflowIds = new List<Guid>();
            List<Workflow> workflows = new List<Workflow>();
            for (int i = 0; i < WorkflowCount; i++)
            {
                CountingWorkflow workflow = new CountingWorkflow();
                workflow.Priority = i % 3;
                workflows.Add(workflow);
                workflowIds.Add(workflow.Id);
            }
            store.Save(workflows);

            List<WorkflowServer> servers = Enumerable.Range(0, 3).Select(x =>
            {
                WorkflowServer server = new WorkflowServer(store, new WorkflowServerOptions() { PersistWorkflowDefinitions = false });
                server.RegisterWorkflowType<CountingWorkflow>();
                return server;
            }).ToList();
            try
            {
                List<string> lostLocks = new List<string>();
                servers.ForEach(s => s.WorkflowLockLost += (sender, e) => lostLocks.Add(e.NodeId));

                Task[] tasks = servers.Select(server => Task.Run(async () =>
                {
                    while ((isAsync ? await server.ExecuteWorkflowsAsync(10, 4) : server.ExecuteWorkflows(10, 4)) > 0)
                    {
                    }
                })).ToArray();
                Task.WaitAll(tasks);

                Assert.That(lostLocks, Is.Empty);
                Assert.That(store.GetActive(WorkflowCount), Is.Empty);
                Assert.That(store.GetLockOwners(), Is.Empty);
                foreach (Guid id in workflowIds)
                {
                    Assert.That(store.GetCompletedOrDefault(id), Is.Not.Null);
                    Assert.That(CountingWorkflow.GetExecutionCount(id, CountingWorkflow.State.StepOne), Is.EqualTo(1));
                    Assert.That(CountingWorkflow.GetExecutionCount(id, CountingWorkflow.State.StepTwo), Is.EqualTo(1));
                }
            }
            finally
            {
                servers.ForEach(s => s.Dispose());
            }
        }

        #endregion

        private static BasicWorkflow CreateClaimableWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-1);
            return workflow;
        }

    }
}
