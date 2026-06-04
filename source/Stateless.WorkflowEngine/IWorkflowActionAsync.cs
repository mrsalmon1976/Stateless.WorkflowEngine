using System.Threading;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowActionAsync
    {
        Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default);
    }
}
