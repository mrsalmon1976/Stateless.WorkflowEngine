using Example.Shared;
using Stateless.WorkflowEngine;
using System.ComponentModel;

namespace Stateless.TestHarness.Multithread
{
    internal class RecordProcessorWorker : BackgroundWorker
    {
        private readonly IWorkflowServer _workflowServer;
        private static object _messageLock = new object();

        public RecordProcessorWorker(IWorkflowServer workflowServer)
        {
            this._workflowServer = workflowServer;
            this.DoWork += MultithreadRecordProcessorWorker_DoWork;
        }

        private void MultithreadRecordProcessorWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            bool workflowsExist = true;

            while (workflowsExist)
            {
                int executed = this._workflowServer.ExecuteWorkflowsAsync(200, 50).GetAwaiter().GetResult();

                if (executed == 0)
                {
                    ConsoleWriter.WriteLine("No workflows found to process, sleeping for 2 seconds");
                    Thread.Sleep(2000);
                }
                else
                {
                    ConsoleWriter.WriteLine($"{executed} workflows executed");
                }

                long activeWorkflowCount = this._workflowServer.GetActiveCount();
                ConsoleWriter.WriteLine($"{activeWorkflowCount} active workflows remain in the store", ConsoleColor.Yellow);
                if (activeWorkflowCount == 0)
                {
                    workflowsExist = false;
                }
            }

            ConsoleWriter.WriteLine($"Done processing workflows");
        }


    }
}
