using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Models.Workflow
{
    public class ConnectionResult
    {
        public Exception Exception { get; set; }

        public IWorkflowProvider WorkflowProvider { get; set; }
    }
}
