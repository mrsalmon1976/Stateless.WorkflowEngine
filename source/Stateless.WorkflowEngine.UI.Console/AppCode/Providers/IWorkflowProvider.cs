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
        IEnumerable<WorkflowContainer> GetActive(int count);
    }
}
