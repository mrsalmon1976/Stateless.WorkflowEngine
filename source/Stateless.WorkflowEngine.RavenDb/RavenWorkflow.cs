using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb
{
    public class RavenWorkflow
    {
        public RavenWorkflow() { }
        public RavenWorkflow(Workflow workflow)
        {
            this.Id = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
            this.Workflow = workflow;
            this.WorkflowType = workflow.GetType().AssemblyQualifiedName;
        }

        public string Id { get; set; }

        public Workflow Workflow { get; set; }

        public string WorkflowType { get; set; }


    }
}
