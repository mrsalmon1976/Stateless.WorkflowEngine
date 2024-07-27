using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.DecreasingPriority.Actions
{
    public class AlterPriorityAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            int newPriority = workflow.Priority - 1; 
            Console.WriteLine($"Altering priority from {workflow.Priority} to {newPriority}");
            workflow.Priority = newPriority;
            workflow.ResumeTrigger = DecreasingPriorityWorkflow.Trigger.Complete.ToString();
        }
    }
}
