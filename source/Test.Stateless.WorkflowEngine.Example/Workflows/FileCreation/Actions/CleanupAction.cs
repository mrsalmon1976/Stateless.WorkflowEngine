using System;
using Stateless.WorkflowEngine;
using System.IO;

namespace Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation.Actions
{
    public class CleanupAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            // clean up all the created files
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            string[] files = Directory.GetFiles(fcw.RootFolder, "__*File*.txt");
            foreach (string f in files)
            {
                File.Delete(f);
            }
            fcw.ResumeTrigger = FileCreationWorkflow.Trigger.Complete.ToString();

        }
    }
}
