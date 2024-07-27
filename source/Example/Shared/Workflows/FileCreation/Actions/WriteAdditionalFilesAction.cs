using System;
using System.IO;
using Stateless.WorkflowEngine;

namespace Example.Shared.Workflows.FileCreation.Actions
{
    public class WriteAdditionalFilesAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            int fileNumber = (fcw.FilesCreatedCount ?? 0) + 1;
            string fileName = $"AdditionalFile_{DateTime.Now.ToString("HHmmss")}_{fileNumber}.txt";
            string filePath = fcw.GetFilePath(Constants.RootPath, fileName);

            ConsoleWriter.WriteLine("WriteAdditionalFilesAction: ", $"Writing file {filePath}", ConsoleColor.Green);
            File.WriteAllText(filePath, "Example workflow");

            if (fileNumber == fcw.FilesToCreateCount)
            {
                ConsoleWriter.WriteLine("WriteAdditionalFilesAction: ", $"All files created moving to cleanup");
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.CleanUp.ToString();
            }
            else
            {
                ConsoleWriter.WriteLine("WriteAdditionalFilesAction: ", $"{fileNumber} of {fcw.FilesToCreateCount} created, re-entering");
                // not done yet - trigger a re-entry
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.WriteAdditionalFiles.ToString();
            }

            ConsoleWriter.WriteLine("WriteAdditionalFilesAction: ", $"Sleeping for 5 seconds");
            fcw.FilesCreatedCount = fileNumber;
            fcw.ResumeOn = DateTime.UtcNow.AddSeconds(5);
        }
    }
}
