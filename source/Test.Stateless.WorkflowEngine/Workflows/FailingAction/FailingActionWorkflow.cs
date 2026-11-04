using System;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.FailingAction
{
    /// <summary>
    /// Workflow whose only action always throws.
    /// </summary>
    public class FailingActionWorkflow : StateWorkflow<FailingActionWorkflow.State, FailingActionWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            Failing
        }

        public enum Trigger
        {
            Fail
        }

        public FailingActionWorkflow() : base(State.Start.ToString(), Trigger.Fail.ToString())
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.Fail, State.Failing);

            this.Configure(State.Failing)
                .OnEntry(() => this.ExecuteWorkflowAction<FailingAction>());
        }

        public class FailingAction : IWorkflowAction
        {
            public void Execute(Workflow workflow)
            {
                throw new InvalidOperationException("The action failed");
            }
        }
    }
}
