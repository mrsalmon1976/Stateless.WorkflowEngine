using Stateless.WorkflowEngine;
using System.ComponentModel;

namespace NugetTestApp
{
    internal class WorkflowServerWorker : BackgroundWorker
    {
        private readonly IWorkflowServer _workflowServer;
        private static object _messageLock = new object();

        public WorkflowServerWorker(IWorkflowServer workflowServer)
        {
            this._workflowServer = workflowServer;
            this.DoWork += WorkflowServerWorker_DoWork;
        }

        private void WorkflowServerWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            int zeroExecutions = 0;

            while (zeroExecutions < 5)
            {
                int executed = this._workflowServer.ExecuteWorkflowsAsync(50, 10).GetAwaiter().GetResult();

                if (executed == 0)
                {
                    ConsoleWriter.WriteLine("No workflows found to process, sleeping for 1 second", ConsoleColor.DarkMagenta);
                    zeroExecutions++;
                    Thread.Sleep(1000);
                }
                else
                {
                    zeroExecutions = 0;
                    ConsoleWriter.WriteLine($"{executed} workflows executed; ", ConsoleColor.Yellow);
                }
            }

            ConsoleWriter.WriteLine($"Done processing workflows", ConsoleColor.Blue);
        }


    }
}
