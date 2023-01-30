using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb
{
    public class RavenCompletedWorkflow
    {
        public RavenCompletedWorkflow() { }

        public RavenCompletedWorkflow(Workflow workflow)
        {
            this.Id = RavenDbIdUtility.FormatCompletedWorkflowId(workflow.Id);
            this.Workflow = workflow;
            this.WorkflowType = workflow.GetType().AssemblyQualifiedName;
        }

        public string Id { get; set; }

        public Workflow Workflow { get; set; }

        public string WorkflowType { get; set; }


    }
}
