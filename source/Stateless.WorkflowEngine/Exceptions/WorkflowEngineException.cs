using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Exceptions
{
    public class WorkflowEngineException : Exception
    {
        public WorkflowEngineException(string message) : base(message)
        {
        
        }

        public WorkflowEngineException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
