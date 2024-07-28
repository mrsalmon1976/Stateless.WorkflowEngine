using NugetTestApp.Workflow.Actions;
using Stateless.WorkflowEngine;

namespace NugetTestApp.Workflow
{
    public class TestWorkflow : StateWorkflow<TestWorkflow.State, TestWorkflow.Trigger>
    {

        public enum State
        {
            Start,
            DoingStuff,
            Complete
        }

        public enum Trigger
        {
            DoStuff,
            Complete
        }

        public TestWorkflow() : this(State.Start, Trigger.DoStuff)
        {
        }

        public int WorkflowNumber { get; set; }

        public TestWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
            this.RetryIntervals = new[] { 5, 10, 15, 10, 10 };
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
