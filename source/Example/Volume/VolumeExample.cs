using Dapper;
using Example.Shared;
using Example.Shared.Workflows.Volume;
using MongoDB.Driver;
using Stateless.TestHarness.Multithread;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;

namespace Example.Volume
{
    internal class VolumeExample
    {
        static volatile bool _isComplete = false;

        static ExampleDbType _dbType;

        public static void Run(ExampleDbType dbType, bool runAsync)
        {
            _dbType = dbType;
            _isComplete = false;
            const int TestRecordCount = 1000;
            var mongoClient = new MongoClient(AppSettings.MongoDbConnectionString);
            var database = mongoClient.GetDatabase(AppSettings.VolumeExampleMongoDbName);
            IWorkflowStore workflowStore = new MongoDbWorkflowStore(database);
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            using IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflowType<VolumeWorkflow>();

            VolumeDbMigrator.Run(dbType);

            RecordCreatorWorker creatorWorker = new RecordCreatorWorker(workflowClient, dbType, TestRecordCount, runAsync);
            creatorWorker.RunWorkerAsync();

            RecordProcessorWorker processWorker = new RecordProcessorWorker(workflowServer, runAsync);
            processWorker.RunWorkerAsync();
            processWorker.RunWorkerCompleted += ProcessWorker_RunWorkerCompleted;

            // read whole lines, so nothing is left in the input buffer for the next menu
            while (!_isComplete)
            {
                if (Console.ReadLine() == null)
                {
                    break;
                }
            }

        }

        private static void ProcessWorker_RunWorkerCompleted(object? sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            _isComplete = true;

            using (var conn = DbHelper.GetConnection(_dbType))
            {
                int processedCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM VolumeTest WHERE IsProcessed = 1");
                int unprocessedCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM VolumeTest WHERE IsProcessed = 0");
                ConsoleWriter.WriteLine($"Processed: {processedCount}, Unprocessed: {unprocessedCount}", ConsoleColor.Cyan);}
    
                ConsoleWriter.WriteLine("All processing complete - hit enter to close...", ConsoleColor.Cyan);
        }
    }
}
