using System.Threading;
using System.Threading.Tasks;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Async.Actions
{
    public class NoDefaultConstructorAsyncAction : IWorkflowActionAsync
    {
        public NoDefaultConstructorAsyncAction(string value)
        {
        }

        public Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
