using Example.Shared;
using Example.Shared.Workflows.FileCreation;
using MongoDB.Driver;
using Raven.Client.Documents;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.Stores;

namespace Example.Client
{
    internal class ClientExample
    {

        private IWorkflowClient CreateWorkflowClient(string storeType)
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

            // this call doesn't need to be there in your code - it's just here for the example so the store gets
            // set up correctly.  Usually this will happen when the server boots
            workflowStore?.Initialise(true, true, true);
            return new WorkflowClient(workflowStore);

        }

        private Workflow CreateWorkflow(int fileCount, string fileNamePrefix)
        {
            FileCreationWorkflow workflow = new FileCreationWorkflow();
            workflow.FilesToCreateCount = fileCount;
            workflow.FileNamePrefix = fileNamePrefix;
            workflow.ResumeTrigger = FileCreationWorkflow.Trigger.WriteFirstFile.ToString();
            return workflow;
        }

        public void Run()
        {
            Console.WriteLine();

            string inputStoreType = Prompts.GetInputStoreType();
            Console.WriteLine();

            IWorkflowClient workflowClient = CreateWorkflowClient(inputStoreType);
            Console.WriteLine();
            Console.WriteLine("Press enter when you are ready to start");
            Console.ReadLine();
            Console.WriteLine("Registering workflow 1");
            workflowClient.Register(this.CreateWorkflow(2, "StatelessExample_02Files_"));
            Console.WriteLine("Registering workflow 2");
            workflowClient.Register(this.CreateWorkflow(5, "StatelessExample_05Files_"));
            Console.WriteLine("Registering workflow 3");
            workflowClient.Register(this.CreateWorkflow(10, "StatelessExample_10Files_"));

            Console.WriteLine("Three workflows have been registed - run the Example.Server project to execute the workflows");
            Console.WriteLine();
            Console.WriteLine("Hit enter to exit");
            Console.WriteLine();
            Console.ReadLine();
            Environment.Exit(0);
        }
    }
}
