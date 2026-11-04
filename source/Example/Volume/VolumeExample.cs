using Dapper;
using Example.Shared;
using Example.Shared.Workflows.Volume;
using Example.Shared.Workflows.VolumeAsync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stateless.TestHarness.Multithread;
using Stateless.WorkflowEngine;

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

            // the engine only logs warnings and errors here, so 1000 workflows don't flood the console
            using ServiceProvider serviceProvider = ExampleServices.Create(Constants.StoreTypeMongoDb, AppSettings.VolumeExampleMongoDbName,
                engine => engine
                    .AddWorkflowType<VolumeWorkflow>()
                    .AddWorkflowType<VolumeAsyncWorkflow>()
                    .AddWorkflowActionsFromAssemblyOf<VolumeWorkflow>(type => ExampleServices.InNamespaceOf<VolumeWorkflow>(type) || ExampleServices.InNamespaceOf<VolumeAsyncWorkflow>(type)),
                LogLevel.Warning,
                // scoped: each action gets its own connection, closed when the action's scope is disposed
                services => services.AddScoped(sp => new VolumeDb(dbType)));
            IWorkflowClient workflowClient = serviceProvider.GetRequiredService<IWorkflowClient>();
            IWorkflowServer workflowServer = serviceProvider.GetRequiredService<IWorkflowServer>();

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
