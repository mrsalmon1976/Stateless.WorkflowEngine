using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels
{
    public class BasicResult
    {
        public BasicResult()
            : this(false)
        {
        }

        public BasicResult(bool success) : this(success, new string[] { })
        {
        }

        public BasicResult(bool success, string message) : this(success, new string[] { message })
        {
        }

        public BasicResult(bool success, string[] messages)
        {
            this.Success = success;
            this.Messages = messages;
        }

        public bool Success { get; set; }

        public string[] Messages { get; set; }
    }
}
