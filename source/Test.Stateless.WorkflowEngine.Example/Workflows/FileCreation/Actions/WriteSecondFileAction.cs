using System;
using Stateless.WorkflowEngine;
using System.IO;

namespace Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation.Actions
{
    public class WriteSecondFileAction : IWorkflowAction
    {

        public void Execute(Workflow workflow)
        {
            FileCreationWorkflow fcw = (FileCreationWorkflow)workflow;
            fcw.SecondFileWriteCount += 1;

            File.WriteAllText(fcw.GetFilePath("__SecondFile"), "Example workflow");

            if (fcw.SecondFileWriteCount == 5)
            {
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.CleanUp.ToString();
            }
            else
            {
                fcw.ResumeTrigger = FileCreationWorkflow.Trigger.WriteFirstFile.ToString();
            }
            fcw.ResumeOn = DateTime.UtcNow.AddSeconds(5);
        }
    }
}
