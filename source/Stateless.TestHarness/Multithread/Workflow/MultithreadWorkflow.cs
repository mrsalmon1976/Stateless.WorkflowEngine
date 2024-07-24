using MongoDB.Bson.Serialization.Attributes;
using Stateless.TestHarness.Multithread.Workflow.Actions;
using Stateless.WorkflowEngine;
using System;
using System.IO;

namespace Stateless.TestHarness.Multithread.Workflow
{
    [BsonIgnoreExtraElements]
    public class MultithreadWorkflow : StateWorkflow<MultithreadWorkflow.State, MultithreadWorkflow.Trigger>
    {

        public enum State
        {
            Start,
            MarkingRecordAsProcessed,
            Complete
        }

        public enum Trigger
        {
            MarkRecordAsProcessed,
            Complete
        }

        public MultithreadWorkflow() : this(State.Start, Trigger.MarkRecordAsProcessed)
        {
        }

        public MultithreadWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
            this.RetryIntervals = new[] { 5, 10, 15, 10, 10 };
        }

        public int RecordId { get; set; }


        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.MarkRecordAsProcessed, State.MarkingRecordAsProcessed);

            this.Configure(State.MarkingRecordAsProcessed)
                .OnEntry(() => this.ExecuteWorkflowAction<MarkRecordAsProcessedAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);

        }

    }
}
