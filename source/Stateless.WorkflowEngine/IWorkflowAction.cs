using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowAction
    {
        void Execute(Workflow workflow);
    }
}
