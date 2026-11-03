using System;
using System.IO;
using Stateless.WorkflowEngine;

namespace Benchmark.Agent
{
    public class WriteFileAction : IWorkflowAction
    {
        public const int FileSize = 100 * 1024;

        public void Execute(Workflow workflow)
        {
            BenchmarkWorkflow wf = (BenchmarkWorkflow)workflow;
            wf.StartedOn = DateTime.UtcNow;

            byte[] data = new byte[FileSize];
            Random.Shared.NextBytes(data);
            wf.FilePath = Path.Combine(AgentSettings.WorkFolder, wf.Id.ToString("N") + ".bin");
            File.WriteAllBytes(wf.FilePath, data);

            wf.ResumeTrigger = BenchmarkWorkflow.Trigger.DeleteFile.ToString();
        }
    }

    public class DeleteFileAction : IWorkflowAction
    {
        public void Execute(Workflow workflow)
        {
            BenchmarkWorkflow wf = (BenchmarkWorkflow)workflow;
            File.Delete(wf.FilePath);
            wf.ResumeTrigger = BenchmarkWorkflow.Trigger.Complete.ToString();
        }
    }
}
