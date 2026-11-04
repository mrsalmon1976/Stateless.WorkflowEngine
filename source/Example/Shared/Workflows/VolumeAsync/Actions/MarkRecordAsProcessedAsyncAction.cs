using System;
using Stateless.WorkflowEngine;
using System.IO;
using System.Threading;
using Dapper;
using System.Data.SQLite;

namespace Example.Shared.Workflows.VolumeAsync.Actions
{
    public class MarkRecordAsProcessedAsyncAction : IWorkflowActionAsync
    {
        const string UpdateSql = "UPDATE VolumeTest SET IsProcessed = 1, ProcessDate = @ProcessDate WHERE Id = @Id";

        const string SelectSql = "SELECT Id from VolumeTest WHERE Id = @Id";

        private readonly VolumeDb _volumeDb;

        public MarkRecordAsProcessedAsyncAction(VolumeDb volumeDb)
        {
            _volumeDb = volumeDb;
        }

        public async Task ExecuteAsync(Stateless.WorkflowEngine.Workflow workflow, CancellationToken cancellationToken = default)
        {
            VolumeAsyncWorkflow mtw = (VolumeAsyncWorkflow)workflow;

            Random r = new Random();
            if (mtw.RetryCount < 3 && r.Next(1, 250) == 100)
            {
                ConsoleWriter.WriteLine($"Workflow for record {mtw.RecordId} raised a (deliberate) random exception - this one will sleep for a bit!", ConsoleColor.Red);
                throw new Exception("Contrived exception to ensure retry works...");
            }

            // the connection belongs to this action's scope, and is closed once the action has executed
            var conn = _volumeDb.Connection;
            using (var tran = conn.BeginTransaction())
            {
                int? id = await conn.QueryFirstOrDefaultAsync<int>(SelectSql, new { Id = mtw.RecordId }, tran);
                if (id == null)
                {
                    throw new Exception($"Record not found, id {mtw.RecordId}");
                }
                await conn.ExecuteAsync(UpdateSql, new { ProcessDate = DateTime.Now, Id = mtw.RecordId }, tran);
                tran.Commit();
            }

            mtw.ResumeTrigger = VolumeAsyncWorkflow.Trigger.Complete.ToString();
            
        }
    }
}
