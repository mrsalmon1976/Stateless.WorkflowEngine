using System.Threading;
using System.Threading.Tasks;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Async.Actions
{
    public class AsyncDoingStuffAction : IWorkflowActionAsync
    {
        public bool WasExecuted { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
