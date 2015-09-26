using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Services.Workflow
{
    public interface IWorkflowProvider
    {
        IEnumerable<UIWorkflowContainer> GetActive(int count);

        WorkflowStoreConnection Connection { get; }

        void UnsuspendWorkflow(Guid id);

        void SuspendWorkflow(Guid id);
    }
}
