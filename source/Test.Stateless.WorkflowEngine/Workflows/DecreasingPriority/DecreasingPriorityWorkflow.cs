using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.DecreasingPriority.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.DecreasingPriority
{
    public class DecreasingPriorityWorkflow : StateWorkflow<DecreasingPriorityWorkflow.State, DecreasingPriorityWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            AlteringPriority,
            Complete
        }

        public enum Trigger
        {
            Start,
            AlterPriority,
            Complete
        }

        public DecreasingPriorityWorkflow()
            : this(State.Start)
        {
        }
        public DecreasingPriorityWorkflow(string initialState)
            : base(initialState)
        {

        }

        public DecreasingPriorityWorkflow(State initialState)
            : this(initialState.ToString())
        {
            
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.AlterPriority, State.AlteringPriority);

            this.Configure(State.AlteringPriority)
                .OnEntry(() => this.ExecuteWorkflowAction<AlterPriorityAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
