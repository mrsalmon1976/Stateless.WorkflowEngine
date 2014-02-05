using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState.Actions;

namespace Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState
{
    public class SimpleTwoStateWorkflow : StateWorkflow<SimpleTwoStateWorkflow.State, SimpleTwoStateWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            LoadingData,
            SendingEmail,
            Complete
        }

        public enum Trigger
        {
            Start,
            LoadData,
            SendEmail,
            Complete
        }

        public SimpleTwoStateWorkflow()
            : this(State.Start)
        {
        }
        public SimpleTwoStateWorkflow(string initialState)
            : base(initialState)
        {

        }

        public SimpleTwoStateWorkflow(State initialState)
            : this(initialState.ToString())
        {
            
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.LoadData, State.LoadingData);

            this.Configure(State.LoadingData)
                .OnEntry(() => this.ExecuteWorkflowAction<LoadDataAction>())
                .Permit(Trigger.SendEmail, State.SendingEmail);

            this.Configure(State.SendingEmail)
                .OnEntry(() => this.ExecuteWorkflowAction<SendEmailAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
