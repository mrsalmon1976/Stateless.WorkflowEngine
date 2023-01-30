using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb
{
    internal class RavenDbIdUtility
    {
        public static string FormatCompletedWorkflowId(Guid workflowId)
        {
            return $"CompletedWorkflow/{workflowId}";
        }

        public static string FormatWorkflowId(Guid workflowId)
        {
            return $"Workflow/{workflowId}";
        }

        public static string FormatWorkflowDefinitionId(Guid workflowDefinitionId)
        {
            return $"WorkflowDefinition/{workflowDefinitionId}";
        }
    }
}
