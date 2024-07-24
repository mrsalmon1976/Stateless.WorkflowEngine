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
    internal class MultithreadRecordCreatorWorker : BackgroundWorker
    {
        private readonly IWorkflowClient _workflowClient;
        private int _recordCount = 0;
        private static object _messageLock = new object();

        public MultithreadRecordCreatorWorker(IWorkflowClient workflowClient, int recordCount)
        {
            this._workflowClient = workflowClient;
            this._recordCount = recordCount;
            this.DoWork += MultithreadRecordCreatorWorker_DoWork;
        }

        private void MultithreadRecordCreatorWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            const string sqlInsert = "INSERT INTO MultithreadTest (IsProcessed, CreateDate) values (0, @CreateDate);SELECT SCOPE_IDENTITY()";
            using (var conn = new SqlConnection(AppSettings.ConnectionString))
            {
                const int ProgressIncrement = 100;
                for (int i = 0; i < _recordCount; i++)
                {
                    int recordId = conn.ExecuteScalar<int>(sqlInsert, new { CreateDate = DateTime.Now });
                    MultithreadWorkflow workflow = new MultithreadWorkflow();
                    workflow.RecordId = recordId;
                    _workflowClient.Register(workflow);
                    if (i > 0 && i % ProgressIncrement == 0)
                    {

                        WriteMessage($"{i} records created");
                        Thread.Sleep(100);
                    }
                }
                conn.Close();
            }
            WriteMessage($"{_recordCount} records created");
            WriteMessage($"Done creating records");
        }

        private static void WriteMessage(string message)
        {
            lock (_messageLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"CREATOR: {message}");
                Console.ResetColor();
            }
        }
    }
}
