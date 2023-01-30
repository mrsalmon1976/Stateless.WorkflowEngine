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
            Console.WriteLine($"CleanupAction ---> cleaning up example files");
            string[] files = Directory.GetFiles(Constants.RootPath, $"{fcw.FileNamePrefix}*.txt");
            foreach (string f in files)
            {
                Console.WriteLine($"CleanupAction ---> Deleting file {f}");
                File.Delete(f);
            }
            fcw.ResumeTrigger = FileCreationWorkflow.Trigger.Complete.ToString();

        }
    }
}
