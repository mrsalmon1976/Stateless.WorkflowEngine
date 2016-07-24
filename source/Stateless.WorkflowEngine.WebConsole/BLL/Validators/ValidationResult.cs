using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Validators
{
    public class ValidationResult
    {
        public ValidationResult() : this(new string[] { })
        {
        }

        public ValidationResult(string message) : this(new string[] { message })
        {
        }

        public ValidationResult(IEnumerable<string> messages)
        {
            this.Messages = new List<string>(messages);
        }

        public bool Success 
        {
            get
            {
                return (this.Messages.Count == 0);
            }
        }

        public List<string> Messages { get; set; }
    }

}
