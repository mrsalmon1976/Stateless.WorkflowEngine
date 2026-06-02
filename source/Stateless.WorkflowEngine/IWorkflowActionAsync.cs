using System.Threading.Tasks;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowActionAsync
    {
        Task ExecuteAsync(Workflow workflow);
    }
}
