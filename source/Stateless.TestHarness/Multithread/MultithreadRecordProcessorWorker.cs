using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Stateless.TestHarness.Multithread.Workflow;
using Stateless.WorkflowEngine;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.TestHarness.Multithread
{
    internal class MultithreadRecordProcessorWorker : BackgroundWorker
    {
        private readonly IWorkflowServer _workflowServer;
        private static object _messageLock = new object();

        public MultithreadRecordProcessorWorker(IWorkflowServer workflowServer)
        {
            this._workflowServer = workflowServer;
            this.DoWork += MultithreadRecordProcessorWorker_DoWork;
        }

        private void MultithreadRecordProcessorWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            int zeroResultExecutions = 0;

            while (zeroResultExecutions < 5)
            {
                int executed = this._workflowServer.ExecuteWorkflowsAsync(50).GetAwaiter().GetResult();

                if (executed == 0)
                {
                    WriteMessage("No workflows found to process, sleeping for 1 second");
                    zeroResultExecutions++;
                    Thread.Sleep(1000);
                }
                else
                {
                    zeroResultExecutions = 0;
                    WriteMessage($"{executed} workflows executed");
                }
            }

            WriteMessage($"Done processing workflows");
        }

        private static void WriteMessage(string message)
        {
            lock (_messageLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"PROCESSOR: {message}");
                Console.ResetColor();
            }
        }
    }
}
