using System;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;

namespace Test.Stateless.WorkflowEngine.Workflows.Basic.Actions
{
    public class DoingStuffAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            Console.WriteLine("Doing stuff");

            workflow.ResumeTrigger = BasicWorkflow.Trigger.Complete.ToString();
        }
    }
}
