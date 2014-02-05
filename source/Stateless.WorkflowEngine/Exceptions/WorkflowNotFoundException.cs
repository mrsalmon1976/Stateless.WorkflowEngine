using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Exceptions
{
    public class WorkflowNotFoundException : Exception
    {
        public WorkflowNotFoundException(string message) : base(message)
        {
        
        }

        public WorkflowNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
