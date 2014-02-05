using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Exceptions
{
    public class WorkflowException : Exception
    {
        public WorkflowException(string message) : base(message)
        {
        
        }

        public WorkflowException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
