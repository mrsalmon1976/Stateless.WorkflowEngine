using Stateless.WorkflowEngine;
using Stateless;
using Test.Stateless.WorkflowEngine.Workflows.Basic.Actions;
using Test.Stateless.WorkflowEngine.Workflows.Delayed.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.Delayed
{
    /// <summary>
    /// Basic workflow that does nothing.
    /// </summary>
    public class DelayedWorkflow : StateWorkflow<DelayedWorkflow.State, DelayedWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            DoingStuff,
            Complete
        }

        public enum Trigger
        {
            Start,
            DoStuff,
            Complete
        }

        public DelayedWorkflow()
            : this(State.Start)
        {
        }

        public DelayedWorkflow(string initialState) : base(initialState)
        {
        }

        public DelayedWorkflow(State initialState) : this(initialState.ToString())
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.DoStuff, State.DoingStuff);

            this.Configure(State.DoingStuff)
                .OnEntry(() => this.ExecuteWorkflowAction<DelayedAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
