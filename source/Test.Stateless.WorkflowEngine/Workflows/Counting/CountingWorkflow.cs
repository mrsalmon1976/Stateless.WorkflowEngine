using Stateless.WorkflowEngine;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Test.Stateless.WorkflowEngine.Workflows.Counting
{
    /// <summary>
    /// Two-step workflow that records how many times each step of each workflow is executed, so tests can verify that
    /// multiple workflow servers never execute the same step twice.
    /// </summary>
    public class CountingWorkflow : StateWorkflow<CountingWorkflow.State, CountingWorkflow.Trigger>
    {
        public enum State
        {
            Start,
            StepOne,
            StepTwo,
            Complete
        }

        public enum Trigger
        {
            StepOne,
            StepTwo,
            Complete
        }

        /// <summary>
        /// Execution counts, keyed by "{workflowId}/{step}".
        /// </summary>
        public static readonly ConcurrentDictionary<string, int> Executions = new ConcurrentDictionary<string, int>();

        public CountingWorkflow() : this(State.Start, Trigger.StepOne)
        {
        }

        public CountingWorkflow(State initialState, Trigger initialTrigger) : base(initialState, initialTrigger)
        {
        }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);

            this.Configure(State.Start)
                .Permit(Trigger.StepOne, State.StepOne);

            this.Configure(State.StepOne)
                .OnEntry(() => this.ExecuteStep(State.StepOne, Trigger.StepTwo))
                .Permit(Trigger.StepTwo, State.StepTwo);

            this.Configure(State.StepTwo)
                .OnEntry(() => this.ExecuteStep(State.StepTwo, Trigger.Complete))
                .Permit(Trigger.Complete, State.Complete);

            this.Configure(State.Complete)
                .OnEntry(() => this.IsComplete = true);
        }

        public static int GetExecutionCount(Guid workflowId, State step)
        {
            return Executions.TryGetValue($"{workflowId}/{step}", out int count) ? count : 0;
        }

        private void ExecuteStep(State step, Trigger nextTrigger)
        {
            Executions.AddOrUpdate($"{this.Id}/{step}", 1, (key, count) => count + 1);

            // give the other servers a chance to trip over this workflow
            Thread.Sleep(Random.Shared.Next(0, 3));
            this.ResumeTrigger = nextTrigger.ToString();
        }
    }
}
