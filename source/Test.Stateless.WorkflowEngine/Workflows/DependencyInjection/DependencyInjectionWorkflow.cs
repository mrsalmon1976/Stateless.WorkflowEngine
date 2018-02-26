using Stateless.WorkflowEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.DependencyInjection
{

    /// <summary>
    /// Basic workflow that does nothing.
    /// </summary>
    public class DependencyInjectionWorkflow : StateWorkflow<DependencyInjectionWorkflow.State, DependencyInjectionWorkflow.Trigger>
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

        public DependencyInjectionWorkflow() : this(State.Start)
        {
        }

        public DependencyInjectionWorkflow(string initialState) : base(initialState)
        {
        }

        public DependencyInjectionWorkflow(State initialState)
            : this(initialState.ToString())
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.DoStuff, State.DoingStuff);

            this.Configure(State.DoingStuff)
                .OnEntry(() => this.ExecuteWorkflowAction<NoDefaultConstructorAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }

        public string BasicMetaData { get; set; }
    }

}
