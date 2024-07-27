using System;
using Stateless.WorkflowEngine;
using System.IO;

namespace Example.Shared.Workflows.FileCreation.Actions
{
    public class CleanupAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            // clean up all the created files
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            ConsoleWriter.WriteLine("CleanupAction: ", "Cleaning up example files");
            string[] files = Directory.GetFiles(Constants.RootPath, $"{fcw.FileNamePrefix}*.txt");
            foreach (string f in files)
            {
                ConsoleWriter.WriteLine("CleanupAction: ", $"Deleting file {f}", ConsoleColor.Red);
                File.Delete(f);
            }
            fcw.ResumeTrigger = FileCreationWorkflow.Trigger.Complete.ToString();

        }
    }
}
