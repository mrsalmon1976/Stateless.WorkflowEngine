using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Exceptions
{
    public class WorkflowNotConfiguredException : Exception
    {
        public WorkflowNotConfiguredException(Type workflowType) : base("Workflow not configured.")
        {
        
        }

    }
}
