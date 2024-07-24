using System;
using Stateless.WorkflowEngine;
using System.IO;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Stateless.TestHarness.Multithread.Workflow.Actions
{
    public class MarkRecordAsProcessedAction : IWorkflowAction
    {

        public void Execute(Stateless.WorkflowEngine.Workflow workflow)
        {
            MultithreadWorkflow mtw = (MultithreadWorkflow)workflow;
            const string sql = "UPDATE MultithreadTest SET IsProcessed = 1, ProcessDate = @ProcessDate WHERE Id = @Id";

            using (var conn = new SqlConnection(AppSettings.ConnectionString))
            {
                conn.Open();
                var tran = conn.BeginTransaction();
                int? id = conn.Query<int>("SELECT Id from MultithreadTest WHERE Id = @Id", new { Id = mtw.RecordId }, tran).FirstOrDefault();
                if (id == null)
                {
                    throw new Exception($"Record not found, id {mtw.RecordId}");
                }

                Random r = new Random();
                if (mtw.RetryCount < 3 && r.Next(1, 10) == 5)
                {
                    throw new Exception("Contrived exception to ensure retry works...");
                }

                conn.Execute(sql, new { ProcessDate = DateTime.Now, Id = mtw.RecordId }, tran);
                tran.Commit();
                conn.Close();
            }

            mtw.ResumeTrigger = MultithreadWorkflow.Trigger.Complete.ToString();

        }
    }
}
