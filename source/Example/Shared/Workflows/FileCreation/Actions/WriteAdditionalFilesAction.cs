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

            Console.WriteLine($"WriteAdditionalFilesAction ---> Writing file {filePath}");
            File.WriteAllText(filePath, "Example workflow");

            if (fileNumber == fcw.FilesToCreateCount)
            {
                Console.WriteLine($"WriteAdditionalFilesAction ---> All files created moving to cleanup");
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.CleanUp.ToString();
            }
            else
            {
                Console.WriteLine($"WriteAdditionalFilesAction ---> {fileNumber} of {fcw.FilesToCreateCount} created, re-entering");
                // not done yet - trigger a re-entry
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.WriteAdditionalFiles.ToString();
            }

            Console.WriteLine($"WriteAdditionalFilesAction ---> sleeping for 5 seconds");
            fcw.FilesCreatedCount = fileNumber;
            fcw.ResumeOn = DateTime.UtcNow.AddSeconds(5);
        }
    }
}
