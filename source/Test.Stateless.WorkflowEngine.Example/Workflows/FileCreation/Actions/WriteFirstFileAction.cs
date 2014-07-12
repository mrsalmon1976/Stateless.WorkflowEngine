using System;
using Stateless.WorkflowEngine;
using System.IO;

namespace Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation.Actions
{
    public class WriteFirstFileAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            File.WriteAllText(fcw.GetFilePath("__FirstFile"), "Example workflow");
            workflow.ResumeTrigger = FileCreationWorkflow.Trigger.WriteSecondFile.ToString();
        }
    }
}
