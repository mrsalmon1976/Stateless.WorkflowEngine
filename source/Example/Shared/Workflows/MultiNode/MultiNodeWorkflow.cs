using Example.Shared.Workflows.MultiNode.Actions;
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine;

namespace Example.Shared.Workflows.MultiNode
{
    /// <summary>
    /// Three-step workflow used by the multi-node stress test.  Each step does real disk I/O, sleeps for a random time
    /// and occasionally throws, and records every execution in the execution ledger so the test can prove that no
    /// step is ever executed by two nodes at the same time.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class MultiNodeWorkflow : StateWorkflow<MultiNodeWorkflow.State, MultiNodeWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            WritingFile,
            TransformingFile,
            Finalising,
            Complete
        }

        public enum Trigger
        {
            WriteFile,
            TransformFile,
            Finalise,
            Complete
        }

        public MultiNodeWorkflow() : this(State.Start, Trigger.WriteFile)
        {
        }

        public MultiNodeWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
            // short retries keep the test quick - exceptions are only thrown for the first 3 attempts, so the workflow
            // never runs out of retries and suspends
            this.RetryIntervals = new[] { 1, 2, 3, 3, 3 };
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.WriteFile, State.WritingFile);

            this.Configure(State.WritingFile)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteFileAction>())
                .Permit(Trigger.TransformFile, State.TransformingFile);

            this.Configure(State.TransformingFile)
                .OnEntry(() => this.ExecuteWorkflowAction<TransformFileAction>())
                .Permit(Trigger.Finalise, State.Finalising);

            this.Configure(State.Finalising)
                .OnEntry(() => this.ExecuteWorkflowAction<FinaliseAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
