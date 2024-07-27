using System;
using Stateless.WorkflowEngine;
using System.IO;
using Dapper;
using System.Data.SQLite;

namespace Example.Shared.Workflows.Volume.Actions
{
    public class MarkRecordAsProcessedAction : IWorkflowAction
    {
        const string UpdateSql = "UPDATE VolumeTest SET IsProcessed = 1, ProcessDate = @ProcessDate WHERE Id = @Id";

        const string SelectSql = "SELECT Id from VolumeTest WHERE Id = @Id";

        public void Execute(Stateless.WorkflowEngine.Workflow workflow)
        {
            VolumeWorkflow mtw = (VolumeWorkflow)workflow;

            Random r = new Random();
            if (mtw.RetryCount < 3 && r.Next(1, 250) == 100)
            {
                ConsoleWriter.WriteLine($"Workflow for record {mtw.RecordId} raised a (deliberate) random exception - this one will sleep for a bit!", ConsoleColor.Red);
                throw new Exception("Contrived exception to ensure retry works...");
            }

            using (var conn = DbHelper.GetConnection(mtw.DbType))
            {
                var tran = conn.BeginTransaction();
                int? id = conn.Query<int>(SelectSql, new { Id = mtw.RecordId }, tran).FirstOrDefault();
                if (id == null)
                {
                    throw new Exception($"Record not found, id {mtw.RecordId}");
                }
                conn.Execute(UpdateSql, new { ProcessDate = DateTime.Now, Id = mtw.RecordId }, tran);
                tran.Commit();
                conn.Close();
            }

            mtw.ResumeTrigger = VolumeWorkflow.Trigger.Complete.ToString();

        }
    }
}
