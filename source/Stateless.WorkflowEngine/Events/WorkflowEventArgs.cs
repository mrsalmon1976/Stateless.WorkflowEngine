using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Events
{
    public class WorkflowEventArgs : EventArgs
    {
        public WorkflowEventArgs(Workflow workflow)
        {
            this.Workflow = workflow;
        }

        public Workflow Workflow { get; set; }
    }
}
