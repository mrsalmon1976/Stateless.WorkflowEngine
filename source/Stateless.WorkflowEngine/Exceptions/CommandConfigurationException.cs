using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Exceptions
{
    public class CommandConfigurationException : Exception
    {
        public CommandConfigurationException(string message) : base(message)
        {
        }

    }
}
