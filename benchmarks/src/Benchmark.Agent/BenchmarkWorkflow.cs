using System;
using MongoDB.Bson.Serialization.Attributes;
using Stateless.WorkflowEngine;

namespace Benchmark.Agent
{
    /// <summary>
    /// Two step workflow used by the benchmarks: the first step writes a temp file of random data, and the second step
    /// deletes it.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class BenchmarkWorkflow : StateWorkflow<BenchmarkWorkflow.State, BenchmarkWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            WritingFile,
            DeletingFile,
            Complete
        }

        public enum Trigger
        {
            WriteFile,
            DeleteFile,
            Complete
        }

        public BenchmarkWorkflow() : base(State.Start, Trigger.WriteFile)
        {
        }

        /// <summary>
        /// Gets/sets when the first step started executing (UTC).
        /// </summary>
        public DateTime? StartedOn { get; set; }

        /// <summary>
        /// Gets/sets the path of the temp file written by the first step.
        /// </summary>
        public string FilePath { get; set; }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.WriteFile, State.WritingFile);

            this.Configure(State.WritingFile)
                .OnEntry(() => this.ExecuteWorkflowAction<WriteFileAction>())
                .Permit(Trigger.DeleteFile, State.DeletingFile);

            this.Configure(State.DeletingFile)
                .OnEntry(() => this.ExecuteWorkflowAction<DeleteFileAction>())
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }
    }
}
