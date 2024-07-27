using Example.Shared;
using Example.Shared.Workflows.FileCreation;
using MongoDB.Driver;
using Raven.Client.Documents;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Server
{
    internal class ServerExample
    {

        private static IWorkflowServer CreateWorkflowServer(string storeType)
        {
            IWorkflowStore? workflowStore = null;
            const string DbName = "StatelessWorkflowExample";

            if (storeType == Constants.StoreTypeMongoDb)
            {
                Console.WriteLine($"Using MongoDb - please ensure an unauthenticated version of MongoDb is running " + Environment.NewLine +
                    $"on 'mongodb://localhost'; collection '{DbName}' will be created.");
                var client = new MongoClient("mongodb://localhost");
                var database = client.GetDatabase(DbName);
                workflowStore = new MongoDbWorkflowStore(database);
            }
            else if (storeType == Constants.StoreTypeRavenDb)
            {
                Console.WriteLine($"Using RavenDb - please ensure RavenDb is running on 'http://localhost:8080'; " + Environment.NewLine +
                    $"database '{DbName}' will be created.");

                IDocumentStore store = new DocumentStore
                {
                    Urls = new String[] { "http://localhost:8080" },
                    Database = DbName
                };
                store.Initialize();

                workflowStore = new RavenDbWorkflowStore(store);
            }
            else
            {
                Console.WriteLine($"Store type '{storeType}' not supported");
                Environment.Exit(0);
            }

            return new WorkflowServer(workflowStore);

        }

        public static void Run()
        {
            string inputStoreType = Prompts.GetInputStoreType();
            Console.WriteLine();

            IWorkflowServer workflowServer = CreateWorkflowServer(inputStoreType);
            // NOTE: register any types - this is currently only necessary for MongoDb
            workflowServer.RegisterWorkflowType<FileCreationWorkflow>();

            Console.WriteLine("Press enter when you are ready to start");
            Console.ReadLine();


            long activeCount = workflowServer.GetActiveCount();
            Console.WriteLine($"{activeCount} active workflows found");

            while (true)
            {
                int executedCount = 0;

                try
                {
                    Console.WriteLine("Executing workflows (2 in parallel).....");
                    executedCount = workflowServer.ExecuteWorkflows(2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error occurred executing workflow: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    Environment.Exit(0);
                }

                if (executedCount == 0)
                {
                    Console.WriteLine("No more workflows found to execute - sleeping for 1 second");
                    Thread.Sleep(1000);
                }

                if (workflowServer.GetActiveCount() == 0)
                {
                    Console.WriteLine("No more active workflows, exiting worker loop");
                    break;
                }
            }

            Console.WriteLine("All workflows have completed or suspended");
            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }
    }
}
