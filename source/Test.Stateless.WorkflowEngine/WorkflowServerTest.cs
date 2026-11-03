using System;
using System.Linq;
using System.Threading;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Delayed;
using Stateless.WorkflowEngine.Services;
using NSubstitute;
using NSubstitute.Core;
using Stateless.WorkflowEngine.Events;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.DecreasingPriority;
using System.Threading.Tasks;
using System.Collections.Generic;
using Test.Stateless.WorkflowEngine.Workflows.Counting;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowServerTest
    {
        #region Constructor Tests

        [Test]
        public void Constructor_NoOptions_DefaultOptionsCreated()
        {
            WorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            Assert.That(workflowServer.Options.AutoCreateIndexes, Is.True);
            Assert.That(workflowServer.Options.AutoCreateTables, Is.True);
        }

        [Test]
        public void Constructor_NullOptions_DefaultOptionsCreated()
        {
            WorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>(), null);
            Assert.That(workflowServer.Options.AutoCreateIndexes, Is.True);
            Assert.That(workflowServer.Options.AutoCreateTables, Is.True);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Constructor_WithOptions_OptionsCorrectlySet(bool autoCreateTables, bool autoCreateIndexes)
        {
            WorkflowServerOptions options = new WorkflowServerOptions();
            options.AutoCreateTables = autoCreateTables;
            options.AutoCreateIndexes = autoCreateIndexes;

            WorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>(), options);

            Assert.That(workflowServer.Options.AutoCreateTables, Is.EqualTo(autoCreateTables));
            Assert.That(workflowServer.Options.AutoCreateIndexes, Is.EqualTo(autoCreateIndexes));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, true, true)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        public void Constructor_InitialisesWorkflowStore(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            WorkflowServerOptions options = new WorkflowServerOptions();
            options.AutoCreateTables = autoCreateTables;
            options.AutoCreateIndexes = autoCreateIndexes;
            options.PersistWorkflowDefinitions = persistWorkflowDefinitions;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();

            WorkflowServer workflowServer = new WorkflowServer(workflowStore, options);

            workflowStore.Received(1).Initialise(autoCreateTables, autoCreateIndexes, persistWorkflowDefinitions);
        }


        [Test]
        public void Constructor_OnCreate_WorkflowRegistrationServiceCreated()
        {
            WorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            Assert.That(workflowServer.WorkflowRegistrationService, Is.Not.Null);
            Assert.That(workflowServer.WorkflowRegistrationService, Is.InstanceOf<WorkflowRegistrationService>());
        }

        [Test]
        public void Constructor_OnCreate_WorkflowExceptionHandlerCreated()
        {
            WorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            Assert.That(workflowServer.WorkflowExceptionHandler, Is.Not.Null);
            Assert.That(workflowServer.WorkflowExceptionHandler, Is.InstanceOf<WorkflowExceptionHandler>());
        }

        #endregion


        #region ExecuteWorkflow / ExecuteWorkflowAsync Tests

        [Test]
        public void ExecuteWorkflow_NullWorkflow_RaisesException()
        {
            IWorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            Action del = () => workflowServer.ExecuteWorkflow(null);
            Assert.Throws<ArgumentNullException>(del);
        }

        [Test]
        public void ExecuteWorkflowAsync_NullWorkflow_RaisesException()
        {
            IWorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            Action del = () => workflowServer.ExecuteWorkflowAsync(null).GetAwaiter().GetResult();
            Assert.Throws<ArgumentNullException>(del);
        }


        [Test]
        public void ExecuteWorkflow_OnExecution_InitialisesAndFiresTriggersToCompletion()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            // assert
            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));

        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnExecution_InitialisesAndFiresTriggersToCompletion()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            // assert
            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));

        }


        [Test]
        public void ExecuteWorkflow_WorkflowIsComplete_DoesNotInitialiseAndFireTriggers()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryCount = -1;
            workflow.IsComplete = true;
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);


            // if this workflow had fired, RetryCOunt would have incremented, and the LastException would have 
            // a value as the workflow always throws an exception
            Assert.That(workflow.LastException, Is.Null.Or.Empty);
            Assert.That(workflow.RetryCount, Is.EqualTo(-1));

            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }

        [Test]
        public async Task ExecuteWorkflowAsync_WorkflowIsComplete_DoesNotInitialiseAndFireTriggers()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryCount = -1;
            workflow.IsComplete = true;
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);


            // if this workflow had fired, RetryCOunt would have incremented, and the LastException would have 
            // a value as the workflow always throws an exception
            Assert.That(workflow.LastException, Is.Null.Or.Empty);
            Assert.That(workflow.RetryCount, Is.EqualTo(-1));

            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }


        [Test]
        public void ExecuteWorkflow_OnSuccessfulExecution_RetryCountIsZero()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);


            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);


            Assert.That(workflow.RetryCount, Is.EqualTo(0));

        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnSuccessfulExecution_RetryCountIsZero()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);


            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);


            Assert.That(workflow.RetryCount, Is.EqualTo(0));

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
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);


            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnCompletion_MovesWorkflowIntoCompletedArchive()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);


            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }


        [Test]
        public void ExecuteWorkflow_OnCompletion_WorkflowCompletedEventRaised()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            bool eventRaised = false;

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.WorkflowCompleted += delegate(object sender, WorkflowEventArgs e)
            {
                eventRaised = true;
            };
            workflowServer.ExecuteWorkflow(workflow);


            Assert.That(eventRaised, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnCompletion_WorkflowCompletedEventRaised()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            bool eventRaised = false;

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.WorkflowCompleted += delegate (object sender, WorkflowEventArgs e)
            {
                eventRaised = true;
            };
            await workflowServer.ExecuteWorkflowAsync(workflow);


            Assert.That(eventRaised, Is.True);
        }


        [Test]
        public void ExecuteWorkflow_OnCompletion_CompletedOn_IsSet()
        {
            DateTime startTime = DateTime.UtcNow;
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);


            DateTime endTime = DateTime.UtcNow;

            Assert.That(workflow.CompletedOn, Is.Not.Null); 
            Assert.That(workflow.CompletedOn, Is.GreaterThanOrEqualTo(startTime));
            Assert.That(workflow.CompletedOn, Is.LessThanOrEqualTo(endTime));

        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnCompletion_CompletedOn_IsSet()
        {
            DateTime startTime = DateTime.UtcNow;
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);


            DateTime endTime = DateTime.UtcNow;

            Assert.That(workflow.CompletedOn, Is.Not.Null);
            Assert.That(workflow.CompletedOn, Is.GreaterThanOrEqualTo(startTime));
            Assert.That(workflow.CompletedOn, Is.LessThanOrEqualTo(endTime));

        }


        [Test]
        public void ExecuteWorkflow_OnCompletion_WorkflowOnCompleteCalled()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = Substitute.For<BasicWorkflow>(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            workflow.When(x => x.Fire("Complete")).Do(x => workflow.IsComplete = true);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            workflow.Received(1).OnComplete();
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnCompletion_WorkflowOnCompleteCalled()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = Substitute.For<BasicWorkflow>(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            workflow.When(x => x.FireAsync("Complete")).Do(x => workflow.IsComplete = true);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            workflow.Received(1).OnComplete();
        }

        [Test]
        public void ExecuteWorkflow_OnStepExceptionAndSingleInstanceWorkflow_CorrectMethodCalled()
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = true;
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            workflowServer.ExecuteWorkflow(workflow);


            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepExceptionAndSingleInstanceWorkflow_CorrectMethodCalled()
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = true;
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            await workflowServer.ExecuteWorkflowAsync(workflow);


            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());
        }


        [Test]
        public void ExecuteWorkflow_OnStepException_StateReset()
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            workflowServer.ExecuteWorkflow(workflow);

            // make sure the property was set
            workflow.Received(1).CurrentState = initialState;
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepException_StateReset()
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            await workflowServer.ExecuteWorkflowAsync(workflow);

            // make sure the property was set
            workflow.Received(1).CurrentState = initialState;
        }


        [Test]
        public void ExecuteWorkflow_OnStepExceptionAndMultipleInstanceWorkflow_CorrectMethodCalled()
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = false;
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            workflowServer.ExecuteWorkflow(workflow);

            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());

        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepExceptionAndMultipleInstanceWorkflow_CorrectMethodCalled()
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = false;
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());

        }


        [Test]
        public void ExecuteWorkflow_OnStepCompletionAndResumeDateInFuture_ExecutesNextStepAndStops()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DelayedWorkflow workflow = new DelayedWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepCompletionAndResumeDateInFuture_ExecutesNextStepAndStops()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DelayedWorkflow workflow = new DelayedWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
        }


        [Test]
        public void ExecuteWorkflow_OnStepCompletionAndResumeDateInPast_ExecutesNextStepToCompletion()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));
            Assert.That(workflow.IsComplete, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepCompletionAndResumeDateInPast_ExecutesNextStepToCompletion()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));
            Assert.That(workflow.IsComplete, Is.True);
        }


        [Test]
        public void ExecuteWorkflow_OnStepCompletionAndPriorityChanged_ExecutesNextStepAndStops()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DecreasingPriorityWorkflow workflow = new DecreasingPriorityWorkflow("Start");
            workflow.Priority = 5;
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DecreasingPriorityWorkflow.Trigger.AlterPriority.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DecreasingPriorityWorkflow.State.AlteringPriority.ToString()));
            Assert.That(workflow.IsComplete, Is.False);
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnStepCompletionAndPriorityChanged_ExecutesNextStepAndStops()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DecreasingPriorityWorkflow workflow = new DecreasingPriorityWorkflow("Start");
            workflow.Priority = 5;
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DecreasingPriorityWorkflow.Trigger.AlterPriority.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DecreasingPriorityWorkflow.State.AlteringPriority.ToString()));
            Assert.That(workflow.IsComplete, Is.False);
        }


        [Test]
        public void ExecuteWorkflow_OnWorkflowSuspension_WorkflowSuspendedEventRaised()
        {
            string initialState = Guid.NewGuid().ToString();
            bool eventRaised = false;

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.RetryIntervals =  new int[] { };
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // make sure the workflow is suspended
            IWorkflowExceptionHandler workflowExceptionHandler = Substitute.For<IWorkflowExceptionHandler>();
            workflowExceptionHandler.WhenForAnyArgs(x => x.HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>())).Do(x => { workflow.IsSuspended = true; });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), workflowExceptionHandler);
            workflowServer.WorkflowSuspended += delegate(object sender, WorkflowEventArgs e) {
                eventRaised = true;
            };
            workflowServer.ExecuteWorkflow(workflow);

            Assert.That(eventRaised, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnWorkflowSuspension_WorkflowSuspendedEventRaised()
        {
            string initialState = Guid.NewGuid().ToString();
            bool eventRaised = false;

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.RetryIntervals = new int[] { };
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // make sure the workflow is suspended
            IWorkflowExceptionHandler workflowExceptionHandler = Substitute.For<IWorkflowExceptionHandler>();
            workflowExceptionHandler.WhenForAnyArgs(x => x.HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>())).Do(x => { workflow.IsSuspended = true; });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), workflowExceptionHandler);
            workflowServer.WorkflowSuspended += delegate (object sender, WorkflowEventArgs e) {
                eventRaised = true;
            };
            await workflowServer.ExecuteWorkflowAsync(workflow);

            Assert.That(eventRaised, Is.True);
        }


        [Test]
        public void ExecuteWorkflow_OnWorkflowError_OnErrorCalled()
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.RetryIntervals = new int[] { };
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            workflowServer.ExecuteWorkflow(workflow);

            workflow.Received(1).OnError(Arg.Any<Exception>());
        }

        [Test]
        public async Task ExecuteWorkflowAsync_OnWorkflowError_OnErrorCalled()
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.RetryIntervals = new int[] { };
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            await workflowServer.ExecuteWorkflowAsync(workflow);

            workflow.Received(1).OnError(Arg.Any<Exception>());
        }

        [Test]
        public async Task ExecuteWorkflowAsync_PassesCancellationTokenToFireAsync()
        {
            CancellationToken expectedToken = new CancellationTokenSource().Token;
            CancellationToken capturedToken = CancellationToken.None;

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.ResumeOn = DateTime.UtcNow.AddHours(1);
            workflow.When(x => x.FireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
                    .Do(x => capturedToken = x.ArgAt<CancellationToken>(1));

            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            store.SaveAsync(Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            IWorkflowServer workflowServer = new WorkflowServer(store);
            await workflowServer.ExecuteWorkflowAsync(workflow, expectedToken);

            Assert.That(capturedToken, Is.EqualTo(expectedToken));
        }

        [Test]
        public async Task ExecuteWorkflowAsync_WhenFireAsyncThrowsOperationCanceledException_WorkflowIsSaved()
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.WhenForAnyArgs(x => x.FireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
                    .Do(_ => { throw new OperationCanceledException(); });

            IWorkflowStore store = Substitute.For<IWorkflowStore>();
            store.SaveAsync(Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            // Use a substitute exception handler so it doesn't re-throw when RetryIntervals is not set
            IWorkflowServer workflowServer = CreateWorkflowServer(store, null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            await workflowServer.ExecuteWorkflowAsync(workflow, new CancellationTokenSource().Token);

            await store.Received(1).SaveAsync(workflow);
        }


        #endregion

        #region ExecuteWorkflows / ExecuteWorkflowsAsync Tests

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_OnDelayedAction_ResumesAfterDelay(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DelayedWorkflow workflow = new DelayedWorkflow(DelayedWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));

            // execute again - nothing should have changed
            result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
            Assert.That(result, Is.EqualTo(0));
            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
			
            // delay and run - should now be complete
            Thread.Sleep(3100);
            result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));
            Assert.That(result, Is.EqualTo(1));
            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompletedOrDefault(workflow.Id), Is.Not.Null);

        }

        [TestCase(true, 3)]
        [TestCase(false, 3)]
        [TestCase(true, 5)]
        [TestCase(false, 5)]
        [TestCase(true, 10)]
        [TestCase(false, 10)]
        [TestCase(true, 50)]
        [TestCase(false, 50)]
        public void ExecuteWorkflows_NoMaxConcurrencySupplied_ExecutesAllLoadedWorkflows(bool isAsync, int executeCount)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            int totalWorkflowCount = (executeCount * 2) + new Random().Next(1, 10);

            for (int i = 0; i < totalWorkflowCount; i++)
            {
                BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
                workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
                workflowStore.Save(workflow);
            }

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(executeCount).Result : workflowServer.ExecuteWorkflows(executeCount));

            // assert - only 'executeCount' workflows should have been executed and move out of the Start state
            Assert.That(result, Is.EqualTo(executeCount));

            int workflowsInStartState = workflowStore.GetActive(Int32.MaxValue).Where(x => x.CurrentState == BasicWorkflow.State.Start.ToString()).Count();
            int expectedWorkflowsUnexecuted = totalWorkflowCount - executeCount;
            Assert.That(expectedWorkflowsUnexecuted, Is.EqualTo(workflowsInStartState));

        }

        [Test]
        public async Task ExecuteWorkflows_CancellationRequested_NoWorkflowsExecuted()
        {
            // set up the store and the workflows
            int totalWorkflowCount = new Random().Next(5, 10);

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            List<Workflow> workflows = new List<Workflow>();
            for (int i = 0; i < totalWorkflowCount; i++)
            {
                BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
                workflows.Add(workflow);
            }
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(workflows);

            CancellationToken cancellationToken = new CancellationToken(true);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            int result = await workflowServer.ExecuteWorkflowsAsync(10, 100, cancellationToken);

            // assert - nothing should have been claimed, as claims would leave workflows locked
            Assert.That(result, Is.EqualTo(0));
            await workflowStore.DidNotReceive().ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>());

        }

        [Test]
        public async Task ExecuteWorkflowsAsync_TokenCancelledWhileWorkflowRuns_ClaimedWorkflowsCompleteAndNoMoreClaimed()
        {
            // the first workflow's save cancels the token and is still in flight when the cancellation is observed.  The
            // next workflow was claimed in the background before the first one ran, so it must still be executed (it is
            // locked by this node), but nothing more is claimed.
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            CancellationTokenSource cts = new CancellationTokenSource();
            List<Workflow> claimed = new List<Workflow>();
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(x =>
            {
                List<Workflow> batch = Enumerable.Range(0, x.ArgAt<int>(1)).Select(i => (Workflow)CreateBasicWorkflow()).ToList();
                lock (claimed)
                {
                    claimed.AddRange(batch);
                }
                return batch;
            });
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(async (callInfo) =>
            {
                cts.Cancel();
                await Task.Delay(100);
                return true;
            });
            workflowStore.ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, new WorkflowServerOptions() { ClaimBatchSize = 1 });

            // execute
            int result = await workflowServer.ExecuteWorkflowsAsync(5, 1, cts.Token);

            // assert - the workflow claimed before the cancellation ran, and nothing was claimed after it
            Assert.That(result, Is.EqualTo(2));
            await workflowStore.Received(2).ClaimNextAsync(Arg.Any<string>(), 1);
            foreach (Workflow workflow in claimed)
            {
                await workflowStore.Received().SaveClaimedAsync(workflow, Arg.Any<string>());
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_LastClaimedWorkflowTaken_NextBatchClaimedWhileWorkflowsExecute(bool isAsync)
        {
            // the first batch's workflows wait (in their save) for the second claim - it must start while they are still
            // executing, rather than when a slot runs out of workflows
            ManualResetEventSlim secondClaimStarted = new ManualResetEventSlim();
            int claims = 0;
            Func<CallInfo, List<Workflow>> claim = x =>
            {
                if (Interlocked.Increment(ref claims) == 2)
                {
                    secondClaimStarted.Set();
                }
                return Enumerable.Range(0, x.ArgAt<int>(1)).Select(i => (Workflow)CreateBasicWorkflow()).ToList();
            };
            System.Collections.Concurrent.ConcurrentBag<bool> overlapped = new System.Collections.Concurrent.ConcurrentBag<bool>();
            Func<CallInfo, bool> save = x =>
            {
                overlapped.Add(secondClaimStarted.Wait(TimeSpan.FromSeconds(5)));
                return true;
            };

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(x => claim(x));
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(x => claim(x));
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => save(x));
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => save(x));
            workflowStore.ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, new WorkflowServerOptions() { ClaimBatchSize = 2 });

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(4, 2).Result : workflowServer.ExecuteWorkflows(4, 2));

            // assert
            Assert.That(result, Is.EqualTo(4));
            Assert.That(overlapped, Is.All.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_BackgroundClaimThrows_ClaimedWorkflowsCompleteThenErrorRaised(bool isAsync)
        {
            List<Workflow> firstBatch = new List<Workflow> { CreateBasicWorkflow(), CreateBasicWorkflow() };
            InvalidOperationException claimError = new InvalidOperationException("claim failed");
            int claims = 0;
            Func<List<Workflow>> claim = () =>
            {
                if (Interlocked.Increment(ref claims) > 1)
                {
                    throw claimError;
                }
                return firstBatch;
            };

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(x => claim());
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(x => claim());
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, new WorkflowServerOptions() { ClaimBatchSize = 2 });

            // execute
            TestDelegate execute = () =>
            {
                if (isAsync)
                {
                    workflowServer.ExecuteWorkflowsAsync(10, 2).GetAwaiter().GetResult();
                }
                else
                {
                    workflowServer.ExecuteWorkflows(10, 2);
                }
            };

            // assert - the claim error is raised, but only after the workflows that were claimed have been executed
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(execute);
            Assert.That(ex, Is.SameAs(claimError));
            foreach (Workflow workflow in firstBatch)
            {
                if (isAsync)
                {
                    workflowStore.Received().SaveClaimedAsync(workflow, Arg.Any<string>());
                }
                else
                {
                    workflowStore.Received().SaveClaimed(workflow, Arg.Any<string>());
                }
            }
        }

        [TestCase(true, 3, 1)]
        [TestCase(false, 3, 1)]
        [TestCase(true, 5, 2)]
        [TestCase(false, 5, 2)]
        [TestCase(true, 10, 3)]
        [TestCase(false, 10, 3)]
        [TestCase(true, 50, 10)]
        [TestCase(false, 50, 10)]
        [TestCase(true, 100, 10)]
        [TestCase(false, 100, 10)]
        public void ExecuteWorkflows_MaxConcurrencySupplied_ExecutesAllLoadedWorkflows(bool isAsync, int executeCount, int maxConcurrency)
        {

            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            int totalWorkflowCount = (executeCount * 2) + new Random().Next(1, 10);

            for (int i = 0; i < totalWorkflowCount; i++)
            {
                BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
                workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
                workflowStore.Save(workflow);
            }

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(executeCount, maxConcurrency).Result : workflowServer.ExecuteWorkflows(executeCount, maxConcurrency));

            // assert - only 'executeCount' workflows should have been executed and move out of the Start state
            Assert.That(result, Is.EqualTo(executeCount));

            int workflowsInStartState = workflowStore.GetActive(Int32.MaxValue).Where(x => x.CurrentState == BasicWorkflow.State.Start.ToString()).Count();
            int expectedWorkflowsUnexecuted = totalWorkflowCount - executeCount;
            Assert.That(expectedWorkflowsUnexecuted, Is.EqualTo(workflowsInStartState));
        }

        [TestCase(true, null, 1, 25, new[] { 10, 10, 5 })]
        [TestCase(false, null, 1, 25, new[] { 10, 10, 5 })]
        [TestCase(true, 3, 2, 8, new[] { 3, 3, 2 })]
        [TestCase(false, 3, 2, 8, new[] { 3, 3, 2 })]
        [TestCase(true, 1, 4, 3, new[] { 1, 1, 1 })]
        [TestCase(false, 1, 4, 3, new[] { 1, 1, 1 })]
        public void ExecuteWorkflows_OnExecute_ClaimsBatchesOfClaimBatchSizeUpToCount(bool isAsync, int? claimBatchSize, int maxConcurrent, int count, int[] expectedBatchSizes)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = CreateBatchClaimingStore();
            WorkflowServerOptions options = new WorkflowServerOptions();
            if (claimBatchSize.HasValue)
            {
                options.ClaimBatchSize = claimBatchSize.Value;
            }
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, options);

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(count, maxConcurrent).Result : workflowServer.ExecuteWorkflows(count, maxConcurrent));

            // assert - batches of ClaimBatchSize (default 10) are claimed until the count is reached
            Assert.That(result, Is.EqualTo(count));
            IEnumerable<int> batchSizes = workflowStore.ReceivedCalls()
                .Where(x => x.GetMethodInfo().Name.StartsWith("ClaimNext"))
                .Select(x => (int)x.GetArguments()[1]);
            Assert.That(batchSizes, Is.EqualTo(expectedBatchSizes));
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        [TestCase(true, -1)]
        [TestCase(false, -1)]
        public void ExecuteWorkflows_ClaimBatchSizeLessThanOne_ThrowsException(bool isAsync, int claimBatchSize)
        {
            IWorkflowStore workflowStore = CreateBatchClaimingStore();
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore, new WorkflowServerOptions() { ClaimBatchSize = claimBatchSize });

            TestDelegate execute = () =>
            {
                if (isAsync)
                {
                    workflowServer.ExecuteWorkflowsAsync(5).GetAwaiter().GetResult();
                }
                else
                {
                    workflowServer.ExecuteWorkflows(5);
                }
            };
            global::Stateless.WorkflowEngine.Exceptions.WorkflowException ex = Assert.Throws<global::Stateless.WorkflowEngine.Exceptions.WorkflowException>(execute);
            Assert.That(ex.Message, Does.Contain("ClaimBatchSize"));
            workflowStore.DidNotReceive().ClaimNext(Arg.Any<string>(), Arg.Any<int>());
        }

        /// <summary>
        /// Creates a store that returns as many new workflows as each claim asks for, and accepts every save and archive.
        /// </summary>
        private IWorkflowStore CreateBatchClaimingStore()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(x => Enumerable.Range(0, x.ArgAt<int>(1)).Select(i => CreateBasicWorkflow()).ToList());
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(x => Enumerable.Range(0, x.ArgAt<int>(1)).Select(i => CreateBasicWorkflow()).ToList());
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            return workflowStore;
        }


        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_ActionWithNoDefaultConstructorAndNoDependencyResolver_ThrowsException(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            DependencyInjectionWorkflow workflow = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DependencyInjectionWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            Assert.That(workflow.RetryCount, Is.EqualTo(0));

            // execute
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(10).Result : workflowServer.ExecuteWorkflows(10));

            // we won't get an error, but check the workflow to see if any error occurred
            Assert.That(result, Is.EqualTo(1));
            Assert.That(workflow.RetryCount, Is.EqualTo(1));
            Assert.That(workflow.LastException.Contains("MissingMethodException"), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_ActionWithNoDefaultConstructorAndDependencyResolver_ExecutesAction(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            DependencyInjectionWorkflow workflow = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DependencyInjectionWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            MyDependencyResolver resolver = new MyDependencyResolver();// Substitute.For<IWorkflowEngineDependencyResolver>();
            //resolver.GetInstance<NoDefaultConstructorAction>().Returns(new NoDefaultConstructorAction("test", 1));
            workflowServer.DependencyResolver = resolver;

            // execute
            Assert.That(resolver.RunCount, Is.EqualTo(0));
            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(10).Result : workflowServer.ExecuteWorkflows(10));
            Assert.That(result, Is.EqualTo(1));
            Assert.That(resolver.RunCount, Is.EqualTo(1));

        }

        #endregion

        #region Multi-node Tests

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_OnFirstExecution_NodeIsRegistered(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            WorkflowServerOptions options = new WorkflowServerOptions() { NodeName = "TestNode" };
            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore, options))
            {
                Assert.That(workflowStore.GetNodes(), Is.Empty);

                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

                Assert.That(result, Is.EqualTo(0));
                WorkflowNode node = workflowStore.GetNodes().Single();
                Assert.That(node.Id, Is.EqualTo(workflowServer.NodeId));
                Assert.That(node.Name, Is.EqualTo("TestNode"));
            }
        }

        [Test]
        public void NodeId_NodeIdSetInOptions_UsesOption()
        {
            WorkflowServerOptions options = new WorkflowServerOptions() { NodeId = "my-node" };
            using (WorkflowServer workflowServer = new WorkflowServer(new MemoryWorkflowStore(), options))
            {
                Assert.That(workflowServer.NodeId, Is.EqualTo("my-node"));
            }
        }

        [Test]
        public void NodeId_TwoServers_HaveDifferentIds()
        {
            WorkflowServerOptions options = new WorkflowServerOptions();
            using (WorkflowServer workflowServer1 = new WorkflowServer(new MemoryWorkflowStore(), options))
            using (WorkflowServer workflowServer2 = new WorkflowServer(new MemoryWorkflowStore(), options))
            {
                Assert.That(workflowServer1.NodeId, Is.Not.EqualTo(workflowServer2.NodeId));
            }
        }

        [Test]
        public void ExecuteWorkflows_NodeTimeoutTooShort_ThrowsException()
        {
            WorkflowServerOptions options = new WorkflowServerOptions()
            {
                HeartbeatInterval = TimeSpan.FromSeconds(10),
                NodeTimeout = TimeSpan.FromSeconds(29)
            };
            using (WorkflowServer workflowServer = new WorkflowServer(new MemoryWorkflowStore(), options))
            {
                Assert.Throws<global::Stateless.WorkflowEngine.Exceptions.WorkflowException>(() => workflowServer.ExecuteWorkflows(1));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_WorkflowWaitsForNextStep_LockReleasedInSave(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            DelayedWorkflow workflow = new DelayedWorkflow(DelayedWorkflow.State.Start);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

                Assert.That(result, Is.EqualTo(1));
                Workflow saved = workflowStore.Get(workflow.Id);
                Assert.That(saved.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
                Assert.That(saved.LockedBy, Is.Null);
                Assert.That(saved.LockedOn, Is.Null);
                Assert.That(workflowStore.GetLockOwners(), Is.Empty);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_ActionThrowsException_LockReleasedInSave(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            DependencyInjectionWorkflow workflow = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            workflow.ResumeTrigger = DependencyInjectionWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

                Assert.That(result, Is.EqualTo(1));
                Assert.That(workflow.RetryCount, Is.EqualTo(1));
                Assert.That(workflow.LockedBy, Is.Null);
                Assert.That(workflowStore.GetLockOwners(), Is.Empty);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_WorkflowContinuesExecuting_LockKeptUntilArchived(bool isAsync)
        {
            BasicWorkflow workflow = CreateBasicWorkflow();
            List<string> lockedByOnSave = new List<string>();
            string lockedByOnArchive = "not archived";

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(x => { workflow.LockedBy = x.ArgAt<string>(0); return new[] { workflow }; }, x => new Workflow[0]);
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(x => { workflow.LockedBy = x.ArgAt<string>(0); return new[] { workflow }; }, x => new Workflow[0]);
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => { lockedByOnSave.Add(x.ArgAt<Workflow>(0).LockedBy); return true; });
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => { lockedByOnSave.Add(x.ArgAt<Workflow>(0).LockedBy); return true; });
            workflowStore.ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => { lockedByOnArchive = x.ArgAt<Workflow>(0).LockedBy; return true; });
            workflowStore.ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(x => { lockedByOnArchive = x.ArgAt<Workflow>(0).LockedBy; return true; });

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

                // the lock is kept for both steps (the second runs immediately, and the workflow then completes), and is
                // cleared on the archived copy
                Assert.That(result, Is.EqualTo(1));
                Assert.That(lockedByOnSave, Is.EqualTo(new[] { workflowServer.NodeId, workflowServer.NodeId }));
                Assert.That(lockedByOnArchive, Is.Null);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_WorkflowClaimedByAnotherNode_RaisesWorkflowLockLostAndStops(bool isAsync)
        {
            BasicWorkflow workflow = CreateBasicWorkflow();

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(new[] { workflow }, new Workflow[0]);
            workflowStore.ClaimNextAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(new[] { workflow }, new Workflow[0]);
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(false);
            workflowStore.SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(false);

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                List<WorkflowLockLostEventArgs> events = new List<WorkflowLockLostEventArgs>();
                workflowServer.WorkflowLockLost += (sender, e) => events.Add(e);

                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

                Assert.That(result, Is.EqualTo(1));
                Assert.That(events.Count, Is.EqualTo(1));
                Assert.That(events[0].Workflow, Is.SameAs(workflow));
                Assert.That(events[0].NodeId, Is.EqualTo(workflowServer.NodeId));

                // the first step's save failed, so the second step must not run
                if (isAsync)
                {
                    workflowStore.Received(1).SaveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>());
                    workflowStore.DidNotReceive().ArchiveClaimedAsync(Arg.Any<Workflow>(), Arg.Any<string>());
                }
                else
                {
                    workflowStore.Received(1).SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>());
                    workflowStore.DidNotReceive().ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>());
                }
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_WorkflowsClaimed_RaisesWorkflowClaimedBeforeExecution(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            BasicWorkflow workflow1 = CreateBasicWorkflow();
            BasicWorkflow workflow2 = CreateBasicWorkflow();
            workflowStore.Save(new[] { workflow1, workflow2 });

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                List<WorkflowClaimedEventArgs> events = new List<WorkflowClaimedEventArgs>();
                List<string> statesWhenClaimed = new List<string>();
                workflowServer.WorkflowClaimed += (sender, e) =>
                {
                    lock (events)
                    {
                        events.Add(e);
                        statesWhenClaimed.Add(e.Workflow.CurrentState);
                    }
                };

                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(10).Result : workflowServer.ExecuteWorkflows(10));

                Assert.That(result, Is.EqualTo(2));
                Assert.That(events.Select(x => x.Workflow.Id), Is.EquivalentTo(new[] { workflow1.Id, workflow2.Id }));
                Assert.That(events.Select(x => x.NodeId).Distinct(), Is.EqualTo(new[] { workflowServer.NodeId }));
                Assert.That(statesWhenClaimed, Is.All.EqualTo(BasicWorkflow.State.Start.ToString()));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_WorkflowClaimedHandlerThrows_WorkflowStillExecutedAndReleased(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            BasicWorkflow workflow = CreateBasicWorkflow();
            workflowStore.Save(workflow);

            using (WorkflowServer workflowServer = new WorkflowServer(workflowStore))
            {
                workflowServer.WorkflowClaimed += (sender, e) => throw new InvalidOperationException("handler failed");

                int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(10).Result : workflowServer.ExecuteWorkflows(10));

                Assert.That(result, Is.EqualTo(1));
                Assert.That(workflowStore.GetCompletedOrDefault(workflow.Id), Is.Not.Null);
                Assert.That(workflowStore.GetLockOwners(), Is.Empty);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_ServerDisposed_NoWorkflowsClaimed(bool isAsync)
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            WorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflows(1);
            workflowServer.Dispose();

            BasicWorkflow workflow = CreateBasicWorkflow();
            workflowStore.Save(workflow);

            int result = (isAsync ? workflowServer.ExecuteWorkflowsAsync(1).Result : workflowServer.ExecuteWorkflows(1));

            Assert.That(result, Is.EqualTo(0));
            Assert.That(workflowStore.Get(workflow.Id).LockedBy, Is.Null);
        }

        [Test]
        public void Dispose_NodeHoldsLocks_LocksReleasedAndNodeRemoved()
        {
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            WorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflows(1);

            // a lock left behind by the node (e.g. a save failed)
            BasicWorkflow workflow = CreateBasicWorkflow();
            workflowStore.Save(workflow);
            workflowStore.ClaimNext(workflowServer.NodeId);

            workflowServer.Dispose();

            Assert.That(workflowStore.GetLockOwners(), Is.Empty);
            Assert.That(workflowStore.GetNodes(), Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflows_MultipleServersSharingStore_EachStepExecutedOnce(bool isAsync)
        {
            const int WorkflowCount = 300;
            MemoryWorkflowStore workflowStore = new MemoryWorkflowStore();
            List<Guid> workflowIds = new List<Guid>();
            for (int i = 0; i < WorkflowCount; i++)
            {
                CountingWorkflow workflow = new CountingWorkflow();
                workflow.Priority = i % 3;
                workflowStore.Save(workflow);
                workflowIds.Add(workflow.Id);
            }

            List<WorkflowServer> servers = Enumerable.Range(0, 4).Select(x => new WorkflowServer(workflowStore)).ToList();
            try
            {
                List<string> lostLocks = new List<string>();
                servers.ForEach(s => s.WorkflowLockLost += (sender, e) => lostLocks.Add(e.NodeId));

                Task[] tasks = servers.Select(server => Task.Run(async () =>
                {
                    while ((isAsync ? await server.ExecuteWorkflowsAsync(20, 5) : server.ExecuteWorkflows(20, 5)) > 0)
                    {
                    }
                })).ToArray();
                Task.WaitAll(tasks);

                Assert.That(lostLocks, Is.Empty);
                Assert.That(workflowStore.GetIncompleteCount(), Is.EqualTo(0));
                Assert.That(workflowStore.GetCompletedCount(), Is.EqualTo(WorkflowCount));
                Assert.That(workflowStore.GetLockOwners(), Is.Empty);
                foreach (Guid id in workflowIds)
                {
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

        #region IsSingleInstanceWorkflowRegistered Tests

        [Test]
        public void IsSingleInstanceWorkflowRegistered_OnExecute_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            IWorkflowServer workflowServer = CreateWorkflowServer(workflowStore, null, regService, Substitute.For<IWorkflowExceptionHandler>());
            workflowServer.IsSingleInstanceWorkflowRegistered<BasicWorkflow>();
            regService.Received(1).IsSingleInstanceWorkflowRegistered<BasicWorkflow>(workflowStore);
        }

        #endregion

        #region IsSingleInstanceWorkflowRegisteredAsync Tests

        [Test]
        public async Task IsSingleInstanceWorkflowRegisteredAsync_OnExecute_UsesService()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();
            regService.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore).Returns(Task.FromResult(false));

            IWorkflowServer workflowServer = CreateWorkflowServer(workflowStore, null, regService, Substitute.For<IWorkflowExceptionHandler>());
            await workflowServer.IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>();

            await regService.Received(1).IsSingleInstanceWorkflowRegisteredAsync<BasicWorkflow>(workflowStore);
        }

        #endregion


        #region OnWorkflowStateEntry Tests

        [Test]
        public void OnWorkflowStateEntry_OnStateChange_Persisted()
        {
            BasicWorkflow basicWorkflow = new BasicWorkflow(BasicWorkflow.State.Start);
            basicWorkflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();

            DecreasingPriorityWorkflow decreasingPriorityWorkflow = new DecreasingPriorityWorkflow(DecreasingPriorityWorkflow.State.Start);
            decreasingPriorityWorkflow.ResumeTrigger = DecreasingPriorityWorkflow.Trigger.AlterPriority.ToString();

            DelayedWorkflow delayedWorkflow = new DelayedWorkflow(DelayedWorkflow.State.Start);
            decreasingPriorityWorkflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();

            // set up the workflow store
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns(new Workflow[] { basicWorkflow, decreasingPriorityWorkflow, delayedWorkflow }, new Workflow[0]);
            workflowStore.SaveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);
            workflowStore.ArchiveClaimed(Arg.Any<Workflow>(), Arg.Any<string>()).Returns(true);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflows(10);

            // assert - should receive
            //  2 for the basic workflow as it will execute both steps
            //  1 for the DecreasingPriorityWorkflow which will change priority and not move on
            //  1 for the Delayed workflow which will change resume time
            workflowStore.Received(2).SaveClaimed(basicWorkflow, Arg.Any<string>());
            workflowStore.Received(1).SaveClaimed(decreasingPriorityWorkflow, Arg.Any<string>());
            workflowStore.Received(1).SaveClaimed(delayedWorkflow, Arg.Any<string>());

        }
        #endregion

        #region RegisterWorkflow Tests

        [Test]
        public void RegisterWorkflow_OnRegister_UsesService()
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowServer workflowServer = CreateWorkflowServer(workflowStore, null, regService, Substitute.For<IWorkflowExceptionHandler>());
            workflowServer.RegisterWorkflow(workflow);

            regService.Received(1).RegisterWorkflow(workflowStore, workflow);

        }

        #endregion

        #region RegisterWorkflowAsync Tests

        [Test]
        public async Task RegisterWorkflowAsync_OnRegister_UsesService()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            IWorkflowRegistrationService regService = Substitute.For<IWorkflowRegistrationService>();
            regService.RegisterWorkflowAsync(workflowStore, Arg.Any<Workflow>()).Returns(Task.CompletedTask);

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            IWorkflowServer workflowServer = CreateWorkflowServer(workflowStore, null, regService, Substitute.For<IWorkflowExceptionHandler>());
            await workflowServer.RegisterWorkflowAsync(workflow);

            await regService.Received(1).RegisterWorkflowAsync(workflowStore, workflow);
        }

        #endregion


        private BasicWorkflow CreateBasicWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            return workflow;
        }

        private IWorkflowServer CreateWorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions workflowServerOptions, IWorkflowRegistrationService workflowRegistrationService, IWorkflowExceptionHandler workflowExceptionHandler)
        {
            WorkflowServer workflowServer = new WorkflowServer(workflowStore, workflowServerOptions);
            workflowServer.WorkflowRegistrationService = workflowRegistrationService;
            workflowServer.WorkflowExceptionHandler = workflowExceptionHandler;
            return workflowServer;
        }

        private class MyDependencyResolver : IWorkflowEngineDependencyResolver
        {
            public int RunCount { get; set; }
            public T GetInstance<T>() where T : class
            {
                RunCount++;

                if (typeof(T) == typeof(NoDefaultConstructorAction))
                {
                    return (T)Convert.ChangeType(new NoDefaultConstructorAction("test", 1), typeof(T));
                }

                throw new NotImplementedException();
            }
        }
    }
}
