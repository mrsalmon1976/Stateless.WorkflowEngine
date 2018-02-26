using Stateless.WorkflowEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions
{
    public class NoDefaultConstructorAction : IWorkflowAction
    {
        public NoDefaultConstructorAction(string something, int otherThing)
        {

        }

        public void Execute(Workflow workflow)
        {
            Console.WriteLine("Doing stuff");

            workflow.ResumeTrigger = DependencyInjectionWorkflow.Trigger.Complete.ToString();
        }
    }
}
