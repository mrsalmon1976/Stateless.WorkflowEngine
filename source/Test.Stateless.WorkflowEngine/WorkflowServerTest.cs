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
using Stateless.WorkflowEngine.Services;
using NSubstitute;
using Stateless.WorkflowEngine.Events;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection;

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


        #region ExecuteWorkflow Tests

        [Test]
        public void ExecuteWorkflow_NullWorkflow_RaisesException()
        {
            IWorkflowServer workflowServer = new WorkflowServer(Substitute.For<IWorkflowStore>());
            TestDelegate del = () => workflowServer.ExecuteWorkflow(null);
            Assert.Throws<NullReferenceException>(del);
        }

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
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.ExecuteWorkflow(workflow);

            Assert.That(workflow.CurrentState, Is.EqualTo(BasicWorkflow.State.Complete.ToString()));

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
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);

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
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);
                    
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
        public void ExecuteWorkflow_OnCompletion_CompletedOn_IsSet()
        {
            DateTime startTime = DateTime.UtcNow;
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.DoingStuff);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
            workflowStore.Save(workflow);

            // execute
            IWorkflowServer workflowEngine = new WorkflowServer(workflowStore);
            workflowEngine.ExecuteWorkflow(workflow);

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

            Assert.That(workflow.CurrentState, Is.EqualTo("Complete"));

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

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            workflowServer.ExecuteWorkflows(5);
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
            Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));

            // execute again - nothing should have changed
            workflowServer.ExecuteWorkflows(5);
            workflow = workflowStore.Get<DelayedWorkflow>(workflow.Id);
			Assert.That(workflow.CurrentState, Is.EqualTo(DelayedWorkflow.State.DoingStuff.ToString()));
			
            // delay and run - should be now be complete
            Thread.Sleep(3100);
            workflowServer.ExecuteWorkflows(5);
            Assert.That(workflowStore.GetOrDefault(workflow.Id), Is.Null);
            Assert.That(workflowStore.GetCompletedOrDefault(workflow.Id), Is.Not.Null);

        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(50)]
        public void ExecuteWorkflows_OnExecution_ReturnsNumberOfWorkflowsExecuted(int activeWorkflowCount)
        {
            const int executeCount = 10;
            int expectedResult = Math.Min(executeCount, activeWorkflowCount);
            // set up the store and the workflows
            IWorkflowStore workflowStore = new MemoryWorkflowStore();

            for (int i = 0; i < activeWorkflowCount; i++)
            {
                BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
                workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
                workflowStore.Save(workflow);
            }

            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

            // execute
            int result = workflowServer.ExecuteWorkflows(executeCount);
            Assert.That(result, Is.EqualTo(expectedResult));

        }

        [Test]
        public void ExecuteWorkflows_ActionWithNoDefaultConstructorAndNoDependencyResolver_ThrowsException()
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
            workflowServer.ExecuteWorkflows(10);

            // we won't get an error, but check the workflow to see if any error occurred
            Assert.That(workflow.RetryCount, Is.EqualTo(1));
            Assert.That(workflow.LastException.Contains("MissingMethodException"), Is.True);
        }

        [Test]
        public void ExecuteWorkflows_ActionWithNoDefaultConstructorAndDependencyResolver_ExecutesAction()
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
            workflowServer.ExecuteWorkflows(10);
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
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();

            // set up the workflow store
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
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
