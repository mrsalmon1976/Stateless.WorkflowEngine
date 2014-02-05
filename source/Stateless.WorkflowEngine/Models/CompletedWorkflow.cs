using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Models
{
    public class CompletedWorkflow : WorkflowContainer
    {
        public CompletedWorkflow()
        {
        }
        public CompletedWorkflow(Workflow workflow) : base(workflow)
        {
            this.CompletedOnUtc = DateTime.UtcNow;
        }

        public DateTime CompletedOnUtc { get; set; }

    }
}
