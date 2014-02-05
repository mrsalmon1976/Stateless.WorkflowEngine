using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState.Actions
{
    public class LoadDataAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            Console.WriteLine("Data loaded");

            workflow.ResumeTrigger = SimpleTwoStateWorkflow.Trigger.SendEmail.ToString();
        }
    }
}
