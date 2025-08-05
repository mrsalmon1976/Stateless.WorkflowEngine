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
        static bool _isComplete = false;

        static ExampleDbType _dbType;

        public static void Run(ExampleDbType dbType, bool runAsync)
        {
            _dbType = dbType;
            const int TestRecordCount = 10000;
            var mongoClient = new MongoClient("mongodb://localhost");
            var database = mongoClient.GetDatabase(AppSettings.VolumeExampleMongoDbName);
            IWorkflowStore workflowStore = new MongoDbWorkflowStore(database);
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflowType<VolumeWorkflow>();

            VolumeDbMigrator.Run(dbType);

            RecordCreatorWorker creatorWorker = new RecordCreatorWorker(workflowClient, dbType, TestRecordCount);
            creatorWorker.RunWorkerAsync();

            RecordProcessorWorker processWorker = new RecordProcessorWorker(workflowServer, runAsync);
            processWorker.RunWorkerAsync();
            processWorker.RunWorkerCompleted += ProcessWorker_RunWorkerCompleted;

            while (!_isComplete)
            {
                Console.Read();
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
