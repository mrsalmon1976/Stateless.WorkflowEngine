using Dapper;
using Example;
using Example.Shared;
using Example.Shared.Workflows.Volume;
using Stateless.WorkflowEngine;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Text;

namespace Stateless.TestHarness.Multithread
{
    internal class RecordCreatorWorker : BackgroundWorker
    {
        private readonly IWorkflowClient _workflowClient;
        private readonly ExampleDbType _dbType;
        private int _recordCount = 0;
        private static object _messageLock = new object();

        private string _sqlInsert = String.Empty;

        public RecordCreatorWorker(IWorkflowClient workflowClient, ExampleDbType dbType, int recordCount)
        {
            this._workflowClient = workflowClient;
            this._dbType = dbType;
            this._recordCount = recordCount;
            this.DoWork += MultithreadRecordCreatorWorker_DoWork;

            _sqlInsert = BuildInsertSql();
            

        }

        private void MultithreadRecordCreatorWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            using (var conn = DbHelper.GetConnection(_dbType))
            {
                var tran = conn.BeginTransaction();
                const int ProgressIncrement = 100;
                for (int i = 0; i < _recordCount; i++)
                {
                    int recordId = conn.ExecuteScalar<int>(_sqlInsert, new { CreateDate = DateTime.Now }, tran, 30);
                    VolumeWorkflow workflow = new VolumeWorkflow();
                    workflow.RecordId = recordId;
                    workflow.DbType = _dbType;
                    _workflowClient.Register(workflow);
                    if (i > 0 && i % ProgressIncrement == 0)
                    {
                        tran.Commit();
                        ConsoleWriter.WriteLine("CREATOR: ", $"{i} records created", ConsoleColor.Green);
                        Thread.Sleep(200);
                        tran = conn.BeginTransaction();
                    }
                }
                tran.Commit();
                conn.Close();
            }
            ConsoleWriter.WriteLine("CREATOR: ", $"{_recordCount} records created", ConsoleColor.Green);
            ConsoleWriter.WriteLine("CREATOR: ", "Done creating records", ConsoleColor.Cyan);
        }

        private string BuildInsertSql()
        {
            StringBuilder sb = new StringBuilder("INSERT INTO VolumeTest (IsProcessed, CreateDate) values (0, @CreateDate);");
            switch (_dbType)
            {
                case ExampleDbType.SqlServer:
                    sb.Append("SELECT SCOPE_IDENTITY()");
                    break;
                case ExampleDbType.Sqlite:
                    sb.Append("SELECT last_insert_rowid()");
                    break;
                default:
                    throw new NotSupportedException();

            }
            return sb.ToString();
        }
       
    }
}
