using Example.Shared.Workflows.VolumeAsync.Actions;
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine;

namespace Example.Shared.Workflows.VolumeAsync
{
    [BsonIgnoreExtraElements]
    public class VolumeAsyncWorkflow : StateWorkflow<VolumeAsyncWorkflow.State, VolumeAsyncWorkflow.Trigger>
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

        public VolumeAsyncWorkflow() : this(State.Start, Trigger.MarkRecordAsProcessed)
        {
        }

        public VolumeAsyncWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
            this.RetryIntervals = new[] { 5, 10, 15, 10, 10 };
        }

        public int RecordId { get; set; }

        public ExampleDbType DbType { get; set; }


        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.MarkRecordAsProcessed, State.MarkingRecordAsProcessed);

            this.Configure(State.MarkingRecordAsProcessed)
                .OnEntryAsync(async () => await this.ExecuteWorkflowActionAsync<MarkRecordAsProcessedAsyncAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete).OnEntry(() => this.IsComplete = true);

        }

    }
}
