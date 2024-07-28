using MongoDB.Driver;
using NugetTestApp;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;

namespace Example.Volume
{
    internal class TestAppRunner
    {
        static bool _isComplete = false;

        public static void Run(string? storeType)
        {
            IWorkflowStore? workflowStore = null;
            if (storeType == "1")
            {
                ConsoleWriter.WriteLine("Using MemoryStore", ConsoleColor.Red);
                workflowStore = new MemoryWorkflowStore();
            }
            else if (storeType == "2")
            {
                ConsoleWriter.WriteLine("Using MongoDbStore", ConsoleColor.Red);
                var mongoClient = new MongoClient("mongodb://localhost");
                //var mongoClient = new MongoClient("mongodb://test:test@localhost/admin");
                var database = mongoClient.GetDatabase("StatelessTest");
                workflowStore = new MongoDbWorkflowStore(database);
            }

            if (workflowStore != null)
            {
                IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
                IWorkflowServer workflowServer = new WorkflowServer(workflowStore);

                WorkflowClientWorker clientWorker = new WorkflowClientWorker(workflowClient, 300);
                clientWorker.RunWorkerAsync();

                WorkflowServerWorker serverWorker = new WorkflowServerWorker(workflowServer);
                serverWorker.RunWorkerAsync();
                serverWorker.RunWorkerCompleted += WorkflowServerWorker_RunWorkerCompleted;

                while (!_isComplete)
                {
                    Console.Read();
                }
            }
            else
            {
                ConsoleWriter.WriteLine("Invalid store type", ConsoleColor.Red);
            }
        }

        private static void WorkflowServerWorker_RunWorkerCompleted(object? sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            _isComplete = true;

            ConsoleWriter.WriteLine("All processing complete - hit enter to close...", ConsoleColor.Cyan);
        }
    }
}
