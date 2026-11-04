using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Diagnostics;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Workflows.Async;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Basic.Actions;
using Test.Stateless.WorkflowEngine.Workflows.FailingAction;
using Metrics = Stateless.WorkflowEngine.Diagnostics.WorkflowEngineDiagnostics.Metrics;
using Tags = Stateless.WorkflowEngine.Diagnostics.WorkflowEngineDiagnostics.Tags;

namespace Test.Stateless.WorkflowEngine.Diagnostics
{
    [TestFixture]
    public class WorkflowEngineInstrumentationTest
    {
        private ServiceProvider _serviceProvider;
        private IMeterFactory _meterFactory;
        private WorkflowEngineInstrumentation _instrumentation;
        private MemoryWorkflowStore _workflowStore;
        private ActivityListener _activityListener;
        private ConcurrentQueue<Activity> _activities;

        [SetUp]
        public void WorkflowEngineInstrumentationTest_SetUp()
        {
            // each test gets its own meter factory, so measurements from other tests aren't collected
            _serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
            _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
            _instrumentation = new WorkflowEngineInstrumentation(_meterFactory);
            _workflowStore = new MemoryWorkflowStore();

            _activities = new ConcurrentQueue<Activity>();
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == WorkflowEngineDiagnostics.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        [TearDown]
        public void WorkflowEngineInstrumentationTest_TearDown()
        {
            _activityListener.Dispose();
            _serviceProvider.Dispose();
        }

        #region Metrics Tests

        [Test]
        public void ExecuteWorkflow_WorkflowCompletes_RecordsTransitionsAndCompletion()
        {
            using MetricCollector<long> transitions = this.CreateCollector<long>(Metrics.Transitions);
            using MetricCollector<double> stepDuration = this.CreateCollector<double>(Metrics.StepDuration);
            using MetricCollector<long> completed = this.CreateCollector<long>(Metrics.Completed);
            using MetricCollector<double> duration = this.CreateCollector<double>(Metrics.Duration);
            BasicWorkflow workflow = this.CreateBasicWorkflow();

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            workflowServer.ExecuteWorkflow(workflow);

            IReadOnlyList<CollectedMeasurement<long>> transitionMeasurements = transitions.GetMeasurementSnapshot();
            Assert.That(transitionMeasurements.Select(x => (x.Tags[Tags.StateFrom], x.Tags[Tags.StateTo], x.Tags[Tags.Trigger])), Is.EqualTo(new[]
            {
                ((object)"Start", (object)"DoingStuff", (object)"DoStuff"),
                ("DoingStuff", "Complete", "Complete")
            }));
            Assert.That(transitionMeasurements.All(x => (string)x.Tags[Tags.WorkflowType] == nameof(BasicWorkflow)));
            Assert.That(stepDuration.GetMeasurementSnapshot().Select(x => x.Tags[Tags.Outcome]), Is.EqualTo(new[] { Tags.OutcomeSuccess, Tags.OutcomeSuccess }));
            Assert.That(completed.GetMeasurementSnapshot().Single().Tags[Tags.WorkflowType], Is.EqualTo(nameof(BasicWorkflow)));
            Assert.That(duration.GetMeasurementSnapshot().Single().Value, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ExecuteWorkflow_ActionSucceeds_RecordsActionDuration()
        {
            using MetricCollector<double> actionDuration = this.CreateCollector<double>(Metrics.ActionDuration);
            BasicWorkflow workflow = this.CreateBasicWorkflow();

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            workflowServer.ExecuteWorkflow(workflow);

            CollectedMeasurement<double> measurement = actionDuration.GetMeasurementSnapshot().Single();
            Assert.That(measurement.Tags[Tags.ActionType], Is.EqualTo(nameof(DoingStuffAction)));
            Assert.That(measurement.Tags[Tags.WorkflowType], Is.EqualTo(nameof(BasicWorkflow)));
            Assert.That(measurement.Tags[Tags.Outcome], Is.EqualTo(Tags.OutcomeSuccess));
            Assert.That(measurement.Tags.ContainsKey(Tags.ErrorType), Is.False);
        }

        [Test]
        public async Task ExecuteActionAsync_ActionSucceeds_RecordsActionDuration()
        {
            using MetricCollector<double> actionDuration = this.CreateCollector<double>(Metrics.ActionDuration);
            TestableAsyncWorkflow workflow = new TestableAsyncWorkflow();
            workflow.Instrumentation = _instrumentation;

            await workflow.RunActionAsync<TestableAsyncWorkflow.OrderTrackingAction>();

            CollectedMeasurement<double> measurement = actionDuration.GetMeasurementSnapshot().Single();
            Assert.That(measurement.Tags[Tags.ActionType], Is.EqualTo(nameof(TestableAsyncWorkflow.OrderTrackingAction)));
            Assert.That(measurement.Tags[Tags.Outcome], Is.EqualTo(Tags.OutcomeSuccess));
        }

        [Test]
        public void ExecuteWorkflow_ActionFails_RecordsRetryAndActionError()
        {
            using MetricCollector<double> actionDuration = this.CreateCollector<double>(Metrics.ActionDuration);
            using MetricCollector<double> stepDuration = this.CreateCollector<double>(Metrics.StepDuration);
            using MetricCollector<long> retried = this.CreateCollector<long>(Metrics.Retried);
            using MetricCollector<long> suspended = this.CreateCollector<long>(Metrics.Suspended);
            using MetricCollector<long> transitions = this.CreateCollector<long>(Metrics.Transitions);
            FailingActionWorkflow workflow = new FailingActionWorkflow();
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            workflowServer.ExecuteWorkflow(workflow);

            string errorType = typeof(InvalidOperationException).FullName;
            CollectedMeasurement<double> action = actionDuration.GetMeasurementSnapshot().Single();
            Assert.That(action.Tags[Tags.Outcome], Is.EqualTo(Tags.OutcomeError));
            Assert.That(action.Tags[Tags.ErrorType], Is.EqualTo(errorType));
            CollectedMeasurement<double> step = stepDuration.GetMeasurementSnapshot().Single();
            Assert.That(step.Tags[Tags.Outcome], Is.EqualTo(Tags.OutcomeError));
            Assert.That(step.Tags[Tags.StateFrom], Is.EqualTo("Start"));
            CollectedMeasurement<long> retry = retried.GetMeasurementSnapshot().Single();
            Assert.That(retry.Tags[Tags.State], Is.EqualTo("Start"));
            Assert.That(retry.Tags[Tags.ErrorType], Is.EqualTo(errorType));
            Assert.That(suspended.GetMeasurementSnapshot(), Is.Empty);
            Assert.That(transitions.GetMeasurementSnapshot(), Is.Empty);
        }

        [Test]
        public void ExecuteWorkflow_ActionFailsOnLastRetry_RecordsSuspended()
        {
            using MetricCollector<long> retried = this.CreateCollector<long>(Metrics.Retried);
            using MetricCollector<long> suspended = this.CreateCollector<long>(Metrics.Suspended);
            FailingActionWorkflow workflow = new FailingActionWorkflow();
            workflow.RetryCount = workflow.RetryIntervals.Length;
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            workflowServer.ExecuteWorkflow(workflow);

            CollectedMeasurement<long> measurement = suspended.GetMeasurementSnapshot().Single();
            Assert.That(measurement.Tags[Tags.WorkflowType], Is.EqualTo(nameof(FailingActionWorkflow)));
            Assert.That(measurement.Tags[Tags.State], Is.EqualTo("Start"));
            Assert.That(retried.GetMeasurementSnapshot(), Is.Empty);
        }

        [Test]
        public async Task RegisterWorkflow_ClientAndServer_RecordsCreated()
        {
            using MetricCollector<long> created = this.CreateCollector<long>(Metrics.Created);
            WorkflowClient workflowClient = new WorkflowClient(_workflowStore, _instrumentation);
            using WorkflowServer workflowServer = this.CreateWorkflowServer();

            workflowClient.Register(new BasicWorkflow());
            await workflowServer.RegisterWorkflowAsync(new FailingActionWorkflow());

            Assert.That(created.GetMeasurementSnapshot().Select(x => x.Tags[Tags.WorkflowType]), Is.EqualTo(new[] { nameof(BasicWorkflow), nameof(FailingActionWorkflow) }));
        }

        [Test]
        public async Task Unsuspend_WorkflowSuspended_RecordsUnsuspended()
        {
            using MetricCollector<long> unsuspended = this.CreateCollector<long>(Metrics.Unsuspended);
            BasicWorkflow workflow = new BasicWorkflow();
            workflow.IsSuspended = true;
            _workflowStore.Save(workflow);
            WorkflowClient workflowClient = new WorkflowClient(_workflowStore, _instrumentation);

            workflowClient.Unsuspend(workflow.Id);
            await workflowClient.UnsuspendAsync(workflow.Id);

            Assert.That(unsuspended.GetMeasurementSnapshot().Count, Is.EqualTo(2));
        }

        #endregion

        #region Activity Tests

        [Test]
        public async Task ExecuteWorkflowAsync_WorkflowCompletes_ActionActivityIsChildOfStep()
        {
            BasicWorkflow workflow = this.CreateBasicWorkflow();

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            await workflowServer.ExecuteWorkflowAsync(workflow);

            List<Activity> activities = this.ActivitiesFor(workflow);
            List<Activity> steps = activities.Where(x => x.OperationName == WorkflowEngineDiagnostics.Activities.Step).OrderBy(x => x.StartTimeUtc).ToList();
            Activity action = activities.Single(x => x.OperationName == WorkflowEngineDiagnostics.Activities.Action);

            Assert.That(steps.Count, Is.EqualTo(2));
            Assert.That(action.ParentSpanId, Is.EqualTo(steps[0].SpanId));
            Assert.That(action.DisplayName, Is.EqualTo(nameof(DoingStuffAction)));
            // consecutive steps are siblings, not nested
            Assert.That(steps[1].ParentSpanId, Is.EqualTo(steps[0].ParentSpanId));
            Assert.That(steps[0].GetTagItem(Tags.StateFrom), Is.EqualTo("Start"));
            Assert.That(steps[0].GetTagItem(Tags.StateTo), Is.EqualTo("DoingStuff"));
            Assert.That(steps[1].Events.Select(x => x.Name), Does.Contain("workflow.completed"));
            Assert.That(activities.All(x => x.Status == ActivityStatusCode.Unset));
        }

        [Test]
        public void ExecuteWorkflow_ActionFailsOnLastRetry_ActivitiesHaveErrorStatus()
        {
            FailingActionWorkflow workflow = new FailingActionWorkflow();
            workflow.RetryCount = workflow.RetryIntervals.Length;
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = this.CreateWorkflowServer();
            workflowServer.ExecuteWorkflow(workflow);

            List<Activity> activities = this.ActivitiesFor(workflow);
            Activity step = activities.Single(x => x.OperationName == WorkflowEngineDiagnostics.Activities.Step);
            Activity action = activities.Single(x => x.OperationName == WorkflowEngineDiagnostics.Activities.Action);
            Assert.That(step.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(step.Events.Select(x => x.Name), Is.SupersetOf(new[] { "exception", "workflow.suspended" }));
            Assert.That(action.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(action.GetTagItem(Tags.ErrorType), Is.EqualTo(typeof(InvalidOperationException).FullName));
        }

        #endregion

        #region DI Tests

        [Test]
        public void AddStatelessWorkflowEngine_WithMeterFactory_RecordsThroughFactoryMeter()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddMetrics();
            services.AddStatelessWorkflowEngine().WithMemoryStore();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            using MetricCollector<long> created = new MetricCollector<long>(serviceProvider.GetRequiredService<IMeterFactory>(), WorkflowEngineDiagnostics.Name, Metrics.Created);

            serviceProvider.GetRequiredService<IWorkflowClient>().Register(new BasicWorkflow());

            Assert.That(serviceProvider.GetRequiredService<WorkflowEngineInstrumentation>(), Is.Not.SameAs(WorkflowEngineInstrumentation.Default));
            Assert.That(created.GetMeasurementSnapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public void AddStatelessWorkflowEngine_WithoutMeterFactory_UsesDefaultInstrumentation()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddStatelessWorkflowEngine().WithMemoryStore();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            WorkflowServer workflowServer = (WorkflowServer)serviceProvider.GetRequiredService<IWorkflowServer>();

            Assert.That(workflowServer.Instrumentation, Is.SameAs(WorkflowEngineInstrumentation.Default));
        }

        #endregion

        #region Private Methods

        private MetricCollector<T> CreateCollector<T>(string instrumentName) where T : struct
        {
            return new MetricCollector<T>(_meterFactory, WorkflowEngineDiagnostics.Name, instrumentName);
        }

        private WorkflowServer CreateWorkflowServer()
        {
            return new WorkflowServer(_workflowStore, null, null, _instrumentation);
        }

        private BasicWorkflow CreateBasicWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            _workflowStore.Save(workflow);
            return workflow;
        }

        private List<Activity> ActivitiesFor(Workflow workflow)
        {
            return _activities.Where(x => (string)x.GetTagItem(Tags.WorkflowId) == workflow.Id.ToString()).ToList();
        }

        #endregion
    }
}
