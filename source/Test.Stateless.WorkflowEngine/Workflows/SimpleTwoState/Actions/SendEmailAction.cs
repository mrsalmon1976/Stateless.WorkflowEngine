using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState.Actions
{
    public class SendEmailAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            Console.WriteLine("Email sent");
            workflow.ResumeTrigger = SimpleTwoStateWorkflow.Trigger.Complete.ToString();
        }
    }
}
