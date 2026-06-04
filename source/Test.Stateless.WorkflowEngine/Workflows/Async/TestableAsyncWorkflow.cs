using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Async
{
    public class TestableAsyncWorkflow : StateWorkflow<TestableAsyncWorkflow.State, TestableAsyncWorkflow.Trigger>
    {
        public enum State { Start }
        public enum Trigger { Start }

        public List<string> CallOrder { get; } = new List<string>();

        public TestableAsyncWorkflow() : base(State.Start.ToString()) { }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);
            this.Configure(State.Start);
        }

        public Task RunActionAsync<T>(CancellationToken cancellationToken = default) where T : class, IWorkflowActionAsync
            => ExecuteWorkflowActionAsync<T>(cancellationToken);

        public override Task OnActionExecutingAsync(IWorkflowActionAsync action, CancellationToken cancellationToken = default)
        {
            CallOrder.Add("executing");
            return Task.CompletedTask;
        }

        public override Task OnActionExecutedAsync(IWorkflowActionAsync action, CancellationToken cancellationToken = default)
        {
            CallOrder.Add("executed");
            return Task.CompletedTask;
        }

        // Nested action that appends to the parent workflow's CallOrder so ordering can be verified.
        public class OrderTrackingAction : IWorkflowActionAsync
        {
            public Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
            {
                ((TestableAsyncWorkflow)workflow).CallOrder.Add("action");
                return Task.CompletedTask;
            }
        }
    }
}
