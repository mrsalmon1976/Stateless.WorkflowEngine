using System;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Delayed.Actions
{
    public class DelayedAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            workflow.ResumeOn = DateTime.UtcNow.AddSeconds(3);
            workflow.ResumeTrigger = DelayedWorkflow.Trigger.Complete.ToString();
        }
    }
}
