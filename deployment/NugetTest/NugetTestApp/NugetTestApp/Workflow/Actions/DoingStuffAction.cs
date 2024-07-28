using System;
using Stateless.WorkflowEngine;

namespace NugetTestApp.Workflow.Actions
{
    public class DoingStuffAction : IWorkflowAction
    {
        public void Execute(Stateless.WorkflowEngine.Workflow workflow)
        {
            TestWorkflow tw = (TestWorkflow)workflow;
            ConsoleWriter.WriteLine($"Workflow {tw.WorkflowNumber} is doing stuff");
            tw.ResumeTrigger = TestWorkflow.Trigger.Complete.ToString();

        }
    }
}
