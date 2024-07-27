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
using Stateless.WorkflowEngine.Events;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Test.Stateless.WorkflowEngine.Workflows.DecreasingPriority;

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

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_NullWorkflow_RaisesException(bool isAsync)
        {
            IWorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            TestDelegate del = () => {
                if (isAsync)
                {
                    workflowServer.ExecuteWorkflowAsync(null).GetAwaiter().GetResult();
                }
                else
                {
                    workflowServer.ExecuteWorkflow(null);
                }
            };
            Assert.Throws<ArgumentNullException>(del);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnExecution_InitialisesAndFiresTriggersToCompletion(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            // assert
            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));

        }


        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_WorkflowIsComplete_DoesNotInitialiseAndFireTriggers(bool isAsync)
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
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            // if this workflow had fired, RetryCOunt would have incremented, and the LastException would have 
            // a value as the workflow always throws an exception
            Assert.That(workflow.LastException, Is.Null.Or.Empty);
            Assert.That(workflow.RetryCount, Is.EqualTo(-1));

            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnSuccessfulExecution_RetryCountIsZero(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);


            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(workflow.RetryCount, Is.EqualTo(0));

        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnCompletion_MovesWorkflowIntoCompletedArchive(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompleted(workflow.Id), Is.Not.Null);

        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnCompletion_WorkflowCompletedEventRaised(bool isAsync)
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
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(eventRaised, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnCompletion_CompletedOn_IsSet(bool isAsync)
        {
            DateTime startTime = DateTime.UtcNow;
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            DateTime endTime = DateTime.UtcNow;

            Assert.That(workflow.CompletedOn, Is.Not.Null); 
            Assert.That(workflow.CompletedOn, Is.GreaterThanOrEqualTo(startTime));
            Assert.That(workflow.CompletedOn, Is.LessThanOrEqualTo(endTime));

        }


        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnCompletion_WorkflowOnCompleteCalled(bool isAsync)
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
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            workflow.Received(1).OnComplete();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepExceptionAndSingleInstanceWorkflow_CorrectMethodCalled(bool isAsync)
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = true;
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepException_StateReset(bool isAsync)
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            // make sure the property was set
            workflow.Received(1).CurrentState = initialState;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepExceptionAndMultipleInstanceWorkflow_CorrectMethodCalled(bool isAsync)
        {
            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.IsSingleInstance = false;
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            IWorkflowExceptionHandler exceptionHandler = Substitute.For<IWorkflowExceptionHandler>();

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), exceptionHandler);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            exceptionHandler.Received(1).HandleWorkflowException(Arg.Any<Workflow>(), Arg.Any<Exception>());

        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepCompletionAndResumeDateInFuture_ExecutesNextStepAndStops(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            DelayedWorkflow workflow = new DelayedWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepCompletionAndResumeDateInPast_ExecutesNextStepToCompletion(bool isAsync)
        {
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow("Start");
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));
            Assert.That(workflow.IsComplete, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnStepCompletionAndPriorityChanged_ExecutesNextStepAndStops(bool isAsync)
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
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(DecreasingPriorityWorkflow.State.AlteringPriority.ToString()));
            Assert.That(workflow.IsComplete, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnWorkflowSuspension_WorkflowSuspendedEventRaised(bool isAsync)
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
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            Assert.That(eventRaised, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExecuteWorkflow_OnWorkflowError_OnErrorCalled(bool isAsync)
        {
            string initialState = Guid.NewGuid().ToString();

            Workflow workflow = Substitute.For<Workflow>();
            workflow.ResumeTrigger = "Test";
            workflow.RetryIntervals = new int[] { };
            workflow.CurrentState.Returns(initialState);
            workflow.WhenForAnyArgs(x => x.Fire(Arg.Any<string>())).Do(x => { throw new Exception(); });

            // execute
            IWorkflowServer workflowServer = CreateWorkflowServer(Substitute.For<IWorkflowStore>(), null, Substitute.For<IWorkflowRegistrationService>(), Substitute.For<IWorkflowExceptionHandler>());
            ExecuteWorkflowTest(isAsync, workflowServer, workflow);

            workflow.Received(1).OnError(Arg.Any<Exception>());
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
            workflowStore.GetActive(Arg.Any<int>()).Returns(new Workflow[] { basicWorkflow, decreasingPriorityWorkflow, delayedWorkflow });

            // execute
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflows(10);

            // assert - should receive
            //  2 for the basic workflow as it will execute both steps
            //  1 for the DecreasingPriorityWorkflow which will change priority and not move on
            //  1 for the Delayed workflow which will change resume time
            workflowStore.Received(2).Save(basicWorkflow);
            workflowStore.Received(1).Save(decreasingPriorityWorkflow);
            workflowStore.Received(1).Save(delayedWorkflow);

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


        private IWorkflowServer CreateWorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions workflowServerOptions, IWorkflowRegistrationService workflowRegistrationService, IWorkflowExceptionHandler workflowExceptionHandler)
        {
            WorkflowServer workflowServer = new WorkflowServer(workflowStore, workflowServerOptions);
            workflowServer.WorkflowRegistrationService = workflowRegistrationService;
            workflowServer.WorkflowExceptionHandler = workflowExceptionHandler;
            return workflowServer;
        }

        private void ExecuteWorkflowTest(bool isAsync, IWorkflowServer workflowServer, Workflow workflow)
        {
            if (isAsync)
            {
                workflowServer.ExecuteWorkflowAsync(workflow).GetAwaiter().GetResult();
            }
            else
            {
                workflowServer.ExecuteWorkflow(workflow);
            }

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
