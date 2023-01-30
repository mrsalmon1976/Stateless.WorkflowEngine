using System;
using System.IO;
using Stateless.WorkflowEngine;

namespace Example.Shared.Workflows.FileCreation.Actions
{
    public class WriteFirstFileAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            Console.WriteLine($"WriteFirstFileAction ---> creating primary file and moving to WriteAdditionalFilesAction");
            string fileName = "PrimaryFile.txt";
            File.WriteAllText(fcw.GetFilePath(Constants.RootPath, fileName), "Example workflow");
            workflow.ResumeTrigger = FileCreationWorkflow.Trigger.WriteAdditionalFiles.ToString();
        }
    }
}
