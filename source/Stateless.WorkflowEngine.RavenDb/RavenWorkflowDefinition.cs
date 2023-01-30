using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb
{
    public class RavenWorkflowDefinition
    {
        public RavenWorkflowDefinition() { }

        public RavenWorkflowDefinition(WorkflowDefinition workflowDefinition)
        {
            this.Id = RavenDbIdUtility.FormatWorkflowDefinitionId(workflowDefinition.Id);
            this.WorkflowDefinition = workflowDefinition;
        }

        public string Id { get; set; }

        public WorkflowDefinition WorkflowDefinition { get; set; }



    }
}
