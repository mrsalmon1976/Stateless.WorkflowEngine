namespace Stateless.WorkflowEngine.Diagnostics
{
    /// <summary>
    /// Names of the metrics and traces emitted by the workflow engine.  Subscribe to them with OpenTelemetry - e.g.
    /// <c>.WithMetrics(m => m.AddMeter(WorkflowEngineDiagnostics.Name))</c> and
    /// <c>.WithTracing(t => t.AddSource(WorkflowEngineDiagnostics.Name))</c> - or any other
    /// <c>System.Diagnostics</c> listener, such as <c>dotnet-counters</c>.
    /// </summary>
    public static class WorkflowEngineDiagnostics
    {
        /// <summary>
        /// The name of the engine's <c>Meter</c> and <c>ActivitySource</c>.
        /// </summary>
        public const string Name = "Stateless.WorkflowEngine";

        /// <summary>
        /// Names of the engine's metric instruments.
        /// </summary>
        public static class Metrics
        {
            /// <summary>Counter: workflows registered with a client or server.</summary>
            public const string Created = "stateless.workflow.created";

            /// <summary>Counter: workflow steps that fired successfully, tagged with the states before and after.</summary>
            public const string Transitions = "stateless.workflow.transitions";

            /// <summary>Histogram (seconds): the time taken to fire a workflow step, whether it succeeded or failed.</summary>
            public const string StepDuration = "stateless.workflow.step.duration";

            /// <summary>Counter: workflows that completed and were archived.</summary>
            public const string Completed = "stateless.workflow.completed";

            /// <summary>Histogram (seconds): the time from a workflow's creation to its completion.</summary>
            public const string Duration = "stateless.workflow.duration";

            /// <summary>Counter: workflow steps that failed, and will be retried.</summary>
            public const string Retried = "stateless.workflow.retried";

            /// <summary>Counter: workflows suspended after failing more times than they have retry intervals.</summary>
            public const string Suspended = "stateless.workflow.suspended";

            /// <summary>Counter: workflows unsuspended with a client.</summary>
            public const string Unsuspended = "stateless.workflow.unsuspended";

            /// <summary>Counter: workflows that could not be saved because another node claimed them.</summary>
            public const string LockLost = "stateless.workflow.lock_lost";

            /// <summary>Histogram (seconds): the time taken to execute a workflow action, whether it succeeded or failed.</summary>
            public const string ActionDuration = "stateless.workflow.action.duration";
        }

        /// <summary>
        /// Names of the engine's activities (spans).
        /// </summary>
        public static class Activities
        {
            /// <summary>A workflow step: firing its trigger, and saving (or archiving) it.</summary>
            public const string Step = "workflow.step";

            /// <summary>A workflow action, within a step.</summary>
            public const string Action = "workflow.action";
        }

        /// <summary>
        /// Names of the tags on the engine's metrics and activities.  The workflow id and node id are only on
        /// activities, as they would give metrics unbounded cardinality.
        /// </summary>
        public static class Tags
        {
            public const string WorkflowId = "workflow.id";
            public const string WorkflowType = "workflow.type";
            public const string State = "workflow.state";
            public const string StateFrom = "workflow.state.from";
            public const string StateTo = "workflow.state.to";
            public const string Trigger = "workflow.trigger";
            public const string RetryCount = "workflow.retry_count";
            public const string NodeId = "workflow.node_id";
            public const string ActionType = "action.type";

            /// <summary><see cref="OutcomeSuccess"/> or <see cref="OutcomeError"/>.</summary>
            public const string Outcome = "outcome";

            /// <summary>The full name of the exception type, when the outcome is an error.</summary>
            public const string ErrorType = "error.type";

            public const string OutcomeSuccess = "success";
            public const string OutcomeError = "error";
        }
    }
}
