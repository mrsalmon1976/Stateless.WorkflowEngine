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
using StructureMap;
using NSubstitute;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowServerTest
    {

        #region ExecuteWorkflow Tests

        [Test]
        public void ExecuteWorkflow_OnExecution_InitialisesAndFiresTriggers()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);

            Assert.AreEqual(BasicWorkflow.State.Complete.ToString(), workflow.CurrentState);

        }

        [Test]
        public void ExecuteWorkflow_OnCompletion_MovesWorkflowIntoCompletedArchive()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);

            Assert.IsNull(workflowStore.GetOrDefault(workflow.Id));
            Assert.IsNotNull(workflowStore.GetCompleted(workflow.Id));

        }

        [Test]
        public void ExecuteWorkflow_OnStepException_IncrementsRetryCountAndContinues()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);
            Thread.Sleep(100);

            Workflow info = workflowStore.Get(workflow.Id);
            Assert.IsNotNullOrEmpty(info.LastException);
            Assert.IsTrue(info.ResumeOn > DateTime.UtcNow);
            Assert.IsTrue(info.ResumeOn < DateTime.UtcNow.AddSeconds(10));
            Assert.IsFalse(info.IsSuspended);
        }

        [Test]
        public void ExecuteWorkflow_OnStepException_RetriesCorrectNumberOfTimesAndThenSuspends()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 0, 0, 0 };
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);

            for (int i = 0; i < workflow.RetryIntervals.Length; i++)
            {
                workflowEngine.ExecuteWorkflow(workflow);
                if (i >= workflow.RetryIntervals.Length - 1)
                {
                    Assert.IsTrue(workflow.IsSuspended);
                }
                else
                {
                    Assert.IsFalse(workflow.IsSuspended);
                }
            }

        }

        [Test]
        public void ExecuteWorkflow_OnStepCompletion_ExecutesNextStep()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);

            Assert.AreEqual("Complete", workflow.CurrentState);

        }

        #endregion

        #region ExecuteWorkflows Tests

        [Test]
        public void ExecuteWorkflows_OnDelayedAction_ResumesAfterDelay()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DelayedWorkflow workflow = new DelayedWorkflow(DelayedWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);

            // execute
            workflowEngine.ExecuteWorkflows(5);
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
            Assert.AreEqual(DelayedWorkflow.State.DoingStuff.ToString(), workflow.CurrentState);

            // execute again - nothing should have changed
            workflowEngine.ExecuteWorkflows(5);
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
            Assert.AreEqual(DelayedWorkflow.State.DoingStuff.ToString(), workflow.CurrentState);

            // delay and run - should be now be complete
            Thread.Sleep(3100);
            workflowEngine.ExecuteWorkflows(5);
            Assert.IsNull(workflowStore.GetOrDefault(workflow.Id));
            Assert.IsNotNull(workflowStore.GetCompletedOrDefault(workflow.Id));

        }


        #endregion

        #region OnWorkflowStateEntry Tests

        [Test]
        public void OnWorkflowStateEntry_OnStateChange_Persisted()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();

            // set up the workflow store
            IWorkflowStore workflowStore = MockUtils.CreateAndRegister<IWorkflowStore>();
            workflowStore.GetActive(Arg.Any<int>()).Returns(new[] { workflow });

            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflows(10);

            // We should have received TWO saves as the workflow moves between the states
            workflowStore.Received(2).Save(workflow);

        }
        #endregion

        #region RegisterWorkflow Tests

        [Test]
        public void RegisterWorkflow_OnRegister_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = MockUtils.CreateAndRegister<IWorkflowStore>();
            IWorkflowRegistrationService regService = MockUtils.CreateAndRegister<IWorkflowRegistrationService>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflow(workflow);

            regService.Received(1).RegisterWorkflow(workflowStore, workflow);

        }

        #endregion


    }
}
