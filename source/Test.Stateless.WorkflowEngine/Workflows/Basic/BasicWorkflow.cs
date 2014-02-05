using Stateless.WorkflowEngine;
using Stateless;
using Test.Stateless.WorkflowEngine.Workflows.Basic.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.Basic
{
    /// <summary>
    /// Basic workflow that does nothing.
    /// </summary>
    public class BasicWorkflow : StateWorkflow<BasicWorkflow.State, BasicWorkflow.Trigger>
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

        public BasicWorkflow() : this(State.Start)
        {
        }

        public BasicWorkflow(string initialState) : base(initialState)
        {
        }

        public BasicWorkflow(State initialState)
            : this(initialState.ToString())
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.DoStuff, State.DoingStuff);

            this.Configure(State.DoingStuff)
                .OnEntry(() => this.ExecuteWorkflowAction<DoingStuffAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
