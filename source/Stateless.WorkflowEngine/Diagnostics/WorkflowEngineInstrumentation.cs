using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using static Stateless.WorkflowEngine.Diagnostics.WorkflowEngineDiagnostics;

namespace Stateless.WorkflowEngine.Diagnostics
{
    /// <summary>
    /// Records the engine's metrics and starts its activities (see <see cref="WorkflowEngineDiagnostics"/>).  Nothing is
    /// recorded unless a listener has subscribed to the meter or activity source.
    /// </summary>
    internal sealed class WorkflowEngineInstrumentation
    {
        private static readonly string Version = typeof(WorkflowEngineInstrumentation).Assembly.GetName().Version?.ToString();

        // bucket boundaries (seconds) for actions and steps, which are usually short, and for whole workflows
        private static readonly double[] ShortDurationBuckets = { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300 };
        private static readonly double[] WorkflowDurationBuckets = { 0.1, 0.5, 1, 5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 21600, 43200, 86400, 259200, 604800 };

        private static readonly Lazy<WorkflowEngineInstrumentation> _default = new Lazy<WorkflowEngineInstrumentation>(() => new WorkflowEngineInstrumentation(new Meter(Name, Version)));

        private readonly Counter<long> _created;
        private readonly Counter<long> _transitions;
        private readonly Histogram<double> _stepDuration;
        private readonly Counter<long> _completed;
        private readonly Histogram<double> _duration;
        private readonly Counter<long> _retried;
        private readonly Counter<long> _suspended;
        private readonly Counter<long> _unsuspended;
        private readonly Counter<long> _lockLost;
        private readonly Histogram<double> _actionDuration;

        /// <summary>
        /// Creates the instrumentation with a meter from the given factory, which owns (and disposes) it.
        /// </summary>
        public WorkflowEngineInstrumentation(IMeterFactory meterFactory) : this(meterFactory.Create(new MeterOptions(Name) { Version = Version }))
        {
        }

        private WorkflowEngineInstrumentation(Meter meter)
        {
            _created = meter.CreateCounter<long>(Metrics.Created, "{workflow}", "Workflows registered for execution");
            _transitions = meter.CreateCounter<long>(Metrics.Transitions, "{transition}", "Workflow steps that fired successfully");
            _stepDuration = meter.CreateHistogram<double>(Metrics.StepDuration, "s", "Time taken to fire a workflow step", advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ShortDurationBuckets });
            _completed = meter.CreateCounter<long>(Metrics.Completed, "{workflow}", "Workflows that completed");
            _duration = meter.CreateHistogram<double>(Metrics.Duration, "s", "Time from a workflow's creation to its completion", advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = WorkflowDurationBuckets });
            _retried = meter.CreateCounter<long>(Metrics.Retried, "{workflow}", "Workflow steps that failed and will be retried");
            _suspended = meter.CreateCounter<long>(Metrics.Suspended, "{workflow}", "Workflows suspended after exhausting their retries");
            _unsuspended = meter.CreateCounter<long>(Metrics.Unsuspended, "{workflow}", "Workflows unsuspended");
            _lockLost = meter.CreateCounter<long>(Metrics.LockLost, "{workflow}", "Workflows not saved because another node claimed them");
            _actionDuration = meter.CreateHistogram<double>(Metrics.ActionDuration, "s", "Time taken to execute a workflow action", advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ShortDurationBuckets });
        }

        /// <summary>
        /// Gets the instrumentation used when none is given (e.g. without dependency injection), with a meter that is
        /// never disposed.
        /// </summary>
        public static WorkflowEngineInstrumentation Default => _default.Value;

        /// <summary>
        /// Gets the source of the engine's activities.  Unlike meters, activity sources aren't created per container.
        /// </summary>
        public static ActivitySource ActivitySource { get; } = new ActivitySource(Name, Version);

        public void WorkflowCreated(Workflow workflow)
        {
            _created.Add(1, new KeyValuePair<string, object>(Tags.WorkflowType, workflow.Name));
        }

        public void WorkflowUnsuspended(Workflow workflow)
        {
            _unsuspended.Add(1, new KeyValuePair<string, object>(Tags.WorkflowType, workflow.Name));
        }

        /// <summary>
        /// Starts the activity for a workflow step, or returns null if nothing is listening.
        /// </summary>
        /// <param name="nodeId">The node that claimed the workflow, or null if it was not claimed.</param>
        public Activity StartStep(Workflow workflow, string nodeId)
        {
            Activity activity = ActivitySource.StartActivity(Activities.Step, ActivityKind.Internal);
            if (activity == null)
            {
                return null;
            }

            activity.DisplayName = $"{workflow.Name} {workflow.ResumeTrigger}";
            if (activity.IsAllDataRequested)
            {
                activity.SetTag(Tags.WorkflowId, workflow.Id.ToString());
                activity.SetTag(Tags.WorkflowType, workflow.Name);
                activity.SetTag(Tags.Trigger, workflow.ResumeTrigger);
                activity.SetTag(Tags.StateFrom, workflow.CurrentState);
                activity.SetTag(Tags.RetryCount, workflow.RetryCount);
                activity.SetTag(Tags.NodeId, nodeId);
            }
            return activity;
        }

        /// <summary>
        /// Records a workflow step that fired successfully, moving the workflow from <paramref name="fromState"/> to its
        /// current state.
        /// </summary>
        public void StepSucceeded(Activity activity, Workflow workflow, string fromState, string trigger, long startTimestamp)
        {
            string toState = workflow.CurrentState;
            _transitions.Add(1, new TagList
            {
                { Tags.WorkflowType, workflow.Name },
                { Tags.StateFrom, fromState },
                { Tags.StateTo, toState },
                { Tags.Trigger, trigger }
            });
            _stepDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
            {
                { Tags.WorkflowType, workflow.Name },
                { Tags.StateFrom, fromState },
                { Tags.Outcome, Tags.OutcomeSuccess }
            });
            activity?.SetTag(Tags.StateTo, toState);
        }

        /// <summary>
        /// Records a workflow step that failed, after the workflow has been set up for a retry or suspended.
        /// </summary>
        public void StepFailed(Activity activity, Workflow workflow, string state, Exception exception, long startTimestamp)
        {
            string errorType = exception.GetType().FullName;
            _stepDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
            {
                { Tags.WorkflowType, workflow.Name },
                { Tags.StateFrom, state },
                { Tags.Outcome, Tags.OutcomeError },
                { Tags.ErrorType, errorType }
            });

            TagList tags = new TagList
            {
                { Tags.WorkflowType, workflow.Name },
                { Tags.State, state },
                { Tags.ErrorType, errorType }
            };
            if (workflow.IsSuspended)
            {
                _suspended.Add(1, tags);
            }
            else
            {
                _retried.Add(1, tags);
            }

            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag(Tags.ErrorType, errorType);
                activity.AddException(exception);
                if (workflow.IsSuspended)
                {
                    activity.AddEvent(new ActivityEvent("workflow.suspended"));
                }
            }
        }

        /// <summary>
        /// Records a workflow that completed and was archived.
        /// </summary>
        public void WorkflowCompleted(Activity activity, Workflow workflow)
        {
            KeyValuePair<string, object> typeTag = new KeyValuePair<string, object>(Tags.WorkflowType, workflow.Name);
            _completed.Add(1, typeTag);
            if (workflow.CompletedOn.HasValue)
            {
                _duration.Record((workflow.CompletedOn.Value - workflow.CreatedOn).TotalSeconds, typeTag);
            }
            activity?.AddEvent(new ActivityEvent("workflow.completed"));
        }

        /// <summary>
        /// Records a workflow that could not be saved because another node claimed it - the event is added to the
        /// current step activity, if there is one.
        /// </summary>
        public void WorkflowLockLost(Workflow workflow)
        {
            _lockLost.Add(1, new KeyValuePair<string, object>(Tags.WorkflowType, workflow.Name));
            Activity activity = Activity.Current;
            if (activity?.Source == ActivitySource && activity.OperationName == Activities.Step)
            {
                activity.AddEvent(new ActivityEvent("workflow.lock_lost"));
            }
        }

        /// <summary>
        /// Starts the activity for a workflow action, or returns null if nothing is listening.
        /// </summary>
        public Activity StartAction(Workflow workflow, object action)
        {
            Activity activity = ActivitySource.StartActivity(Activities.Action, ActivityKind.Internal);
            if (activity == null)
            {
                return null;
            }

            string actionType = action.GetType().Name;
            activity.DisplayName = actionType;
            if (activity.IsAllDataRequested)
            {
                activity.SetTag(Tags.WorkflowId, workflow.Id.ToString());
                activity.SetTag(Tags.WorkflowType, workflow.Name);
                activity.SetTag(Tags.ActionType, actionType);
            }
            return activity;
        }

        /// <summary>
        /// Records a workflow action that executed, with the exception it threw if it failed.
        /// </summary>
        public void ActionExecuted(Activity activity, Workflow workflow, object action, long startTimestamp, Exception exception)
        {
            TagList tags = new TagList
            {
                { Tags.WorkflowType, workflow.Name },
                { Tags.ActionType, action.GetType().Name },
                { Tags.Outcome, exception == null ? Tags.OutcomeSuccess : Tags.OutcomeError }
            };
            if (exception != null)
            {
                tags.Add(Tags.ErrorType, exception.GetType().FullName);
            }
            _actionDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);

            if (activity != null && exception != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag(Tags.ErrorType, exception.GetType().FullName);
                activity.AddException(exception);
            }
        }
    }
}
