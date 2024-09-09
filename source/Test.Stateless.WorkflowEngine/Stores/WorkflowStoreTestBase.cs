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
using System.IO;

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

        #region Get Tests

        [Test]
        public void Get_InvalidId_ThrowsException()
        {
            IWorkflowStore store = GetStore();
            TestDelegate del = () => store.Get(Guid.NewGuid());
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuspended, Is.False);
            Assert.That(result.RetryCount, Is.EqualTo(0));
            Assert.That(result.ResumeOn, Is.GreaterThan(beforeSuspend.AddMilliseconds(-1)));
			Assert.That(result.ResumeOn, Is.LessThan(afterSuspend.AddMilliseconds(1)));
        }

        #endregion


    }
}
