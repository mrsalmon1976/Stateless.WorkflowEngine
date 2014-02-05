using System;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Broken
{
    /// <summary>
    /// Basic workflow that does nothing.
    /// </summary>
    public class BrokenWorkflow : StateWorkflow<BrokenWorkflow.State, BrokenWorkflow.Trigger>
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

        public BrokenWorkflow() : this(State.Start)
        {
        }

        public BrokenWorkflow(string initialState) : base(initialState)
        {
        }

        public BrokenWorkflow(State initialState) : this(initialState.ToString())
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.DoStuff, State.DoingStuff);

            this.Configure(State.DoingStuff)
                .OnEntry(() => { throw new Exception("This workflow is broken"); })
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
