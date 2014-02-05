using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Models
{
    public class WorkflowContainer
    {
        public WorkflowContainer() { }
        public WorkflowContainer(Workflow workflow)
        {
            this.Id = workflow.Id;
            this.Workflow = workflow;
            this.WorkflowType = workflow.GetType().AssemblyQualifiedName;
        }

        public Guid Id { get; set; }

        public Workflow Workflow { get; set; }

        public string WorkflowType { get; set; }
    }
}
