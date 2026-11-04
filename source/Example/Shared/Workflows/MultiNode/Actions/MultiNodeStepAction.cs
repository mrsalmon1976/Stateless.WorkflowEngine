using Example.MultiNode;
using Stateless.WorkflowEngine;
using System.Security.Cryptography;

namespace Example.Shared.Workflows.MultiNode.Actions
{
    /// <summary>
    /// Base class for the multi-node workflow steps.  Every execution is recorded in the execution ledger (start, end
    /// and outcome), and the ledger flags straight away if another node is running the same step at the same time.
    /// </summary>
    public abstract class MultiNodeStepAction : IWorkflowAction
    {
        private readonly MultiNodeContext _context;

        protected MultiNodeStepAction(MultiNodeContext context)
        {
            _context = context;
        }

        protected abstract string StepName { get; }

        protected abstract MultiNodeWorkflow.Trigger NextTrigger { get; }

        public void Execute(Workflow workflow)
        {
            MultiNodeContext context = _context;
            string nodeId = workflow.LockedBy ?? "(unclaimed)";
            var execution = context.Ledger.BeginStep(workflow.Id, this.StepName, context.ProcessKey, context.NodeName, nodeId, workflow.RetryCount);
            if (execution.OverlappingNodes.Count > 0)
            {
                context.Log($"OVERLAP: workflow {workflow.Id} step {this.StepName} is also running on {String.Join(", ", execution.OverlappingNodes)}", ConsoleColor.Red);
            }

            bool success = false;
            string? error = null;
            try
            {
                // only throw for the first 3 attempts, so workflows never run out of retries and suspend
                bool canThrow = workflow.RetryCount < 3;
                if (canThrow && Random.Shared.NextDouble() < context.ErrorRate / 2)
                {
                    throw new InvalidOperationException($"Deliberate exception before the I/O of step {this.StepName}");
                }

                this.DoWork(workflow, context, nodeId);

                int delay = Random.Shared.Next(context.MinDelayMs, context.MaxDelayMs + 1);
                if (Random.Shared.NextDouble() < context.SlowStepRate)
                {
                    // longer than the node timeout, to prove that a long action keeps its lock while the node is alive
                    delay = (int)context.SlowStepDuration.TotalMilliseconds;
                    context.Log($"Workflow {workflow.Id} step {this.StepName} is running slowly ({delay / 1000}s)", ConsoleColor.DarkYellow);
                }
                Thread.Sleep(delay);

                if (canThrow && Random.Shared.NextDouble() < context.ErrorRate / 2)
                {
                    throw new InvalidOperationException($"Deliberate exception after the I/O of step {this.StepName}");
                }

                workflow.ResumeTrigger = this.NextTrigger.ToString();
                success = true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                throw;
            }
            finally
            {
                context.Ledger.EndStep(execution.ExecutionId, workflow.Id, this.StepName, success, error);
                context.Stats.RecordStep(success);
            }
        }

        protected abstract void DoWork(Workflow workflow, MultiNodeContext context, string nodeId);

        /// <summary>
        /// Writes a file of random data, flushed through to disk, then reads it back and verifies its hash.
        /// </summary>
        protected static void WriteAndVerifyFile(string path, int sizeKb)
        {
            byte[] data = RandomNumberGenerator.GetBytes(sizeKb * 1024);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(data, 0, data.Length);
            }

            byte[] readBack = File.ReadAllBytes(path);
            if (!SHA256.HashData(readBack).AsSpan().SequenceEqual(SHA256.HashData(data)))
            {
                throw new IOException($"File {path} did not contain the data that was written");
            }
        }

        protected static string GetWorkflowFolder(MultiNodeContext context, Workflow workflow)
        {
            return Path.Combine(context.WorkFolder, workflow.Id.ToString());
        }
    }

    public class WriteFileAction : MultiNodeStepAction
    {
        public WriteFileAction(MultiNodeContext context) : base(context)
        {
        }

        protected override string StepName => "WriteFile";

        protected override MultiNodeWorkflow.Trigger NextTrigger => MultiNodeWorkflow.Trigger.TransformFile;

        protected override void DoWork(Workflow workflow, MultiNodeContext context, string nodeId)
        {
            WriteAndVerifyFile(Path.Combine(GetWorkflowFolder(context, workflow), $"{this.StepName}-{nodeId}.dat"), context.IoKb);
        }
    }

    public class TransformFileAction : MultiNodeStepAction
    {
        public TransformFileAction(MultiNodeContext context) : base(context)
        {
        }

        protected override string StepName => "TransformFile";

        protected override MultiNodeWorkflow.Trigger NextTrigger => MultiNodeWorkflow.Trigger.Finalise;

        protected override void DoWork(Workflow workflow, MultiNodeContext context, string nodeId)
        {
            // read everything the earlier step wrote, then write this step's own file
            string folder = GetWorkflowFolder(context, workflow);
            foreach (string file in Directory.Exists(folder) ? Directory.GetFiles(folder) : Array.Empty<string>())
            {
                File.ReadAllBytes(file);
            }
            WriteAndVerifyFile(Path.Combine(folder, $"{this.StepName}-{nodeId}.dat"), context.IoKb);
        }
    }

    public class FinaliseAction : MultiNodeStepAction
    {
        public FinaliseAction(MultiNodeContext context) : base(context)
        {
        }

        protected override string StepName => "Finalise";

        protected override MultiNodeWorkflow.Trigger NextTrigger => MultiNodeWorkflow.Trigger.Complete;

        protected override void DoWork(Workflow workflow, MultiNodeContext context, string nodeId)
        {
            string folder = GetWorkflowFolder(context, workflow);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
