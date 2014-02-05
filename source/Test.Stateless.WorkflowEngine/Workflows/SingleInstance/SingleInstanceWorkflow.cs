using Stateless.WorkflowEngine;
using Stateless;
using Test.Stateless.WorkflowEngine.Workflows.Basic.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.SingleInstance
{
    /// <summary>
    /// Simple single-instance workflow that does nothing.
    /// </summary>
    public class SingleInstanceWorkflow : StateWorkflow<SingleInstanceWorkflow.State, SingleInstanceWorkflow.Trigger>
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

        public SingleInstanceWorkflow()
            : this(State.Start)
        {
        }

        public SingleInstanceWorkflow(string initialState)
            : base(initialState)
        {
            this.IsSingleInstance = true;
        }

        public SingleInstanceWorkflow(State initialState) : this(initialState.ToString())
        {
            this.IsSingleInstance = true;
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.DoStuff, State.DoingStuff);

            this.Configure(State.DoingStuff)
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
