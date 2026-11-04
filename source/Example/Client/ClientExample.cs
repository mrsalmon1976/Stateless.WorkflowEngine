using Example.Shared;
using Example.Shared.Workflows.FileCreation;
using Microsoft.Extensions.DependencyInjection;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;

namespace Example.Client
{
    internal class ClientExample
    {

        private static ServiceProvider CreateServiceProvider(string storeType)
        {
            ExampleServices.WriteStoreInfo(storeType, Constants.ExampleDbName);

            // the client only registers workflows, so it needs no workflow types or actions
            ServiceProvider serviceProvider = ExampleServices.Create(storeType, Constants.ExampleDbName);

            // this call doesn't need to be there in your code - it's just here for the example so the store gets
            // set up correctly.  Usually this will happen when the server boots
            serviceProvider.GetRequiredService<IWorkflowStore>().Initialise(true, true, true);
            return serviceProvider;
        }

        private static Workflow CreateWorkflow(int fileCount, string fileNamePrefix)
        {
            FileCreationWorkflow workflow = new FileCreationWorkflow();
            workflow.FilesToCreateCount = fileCount;
            workflow.FileNamePrefix = fileNamePrefix;
            workflow.ResumeTrigger = FileCreationWorkflow.Trigger.WriteFirstFile.ToString();
            return workflow;
        }

        public static void Run()
        {
            Console.WriteLine();

            string inputStoreType = Prompts.GetInputStoreType();
            if (string.IsNullOrEmpty(inputStoreType))
                return;

            Console.WriteLine();

            using ServiceProvider serviceProvider = CreateServiceProvider(inputStoreType);
            IWorkflowClient workflowClient = serviceProvider.GetRequiredService<IWorkflowClient>();
            Console.WriteLine();
            Console.WriteLine("Press enter when you are ready to start");
            Console.ReadLine();
            Console.WriteLine("Registering workflow 1");
            workflowClient.Register(CreateWorkflow(2, "StatelessExample_02Files_"));
            Console.WriteLine("Registering workflow 2");
            workflowClient.Register(CreateWorkflow(5, "StatelessExample_05Files_"));
            Console.WriteLine("Registering workflow 3");
            workflowClient.Register(CreateWorkflow(10, "StatelessExample_10Files_"));

            Console.WriteLine("Three workflows have been registered - run the Example.Server project to execute the workflows");
            Console.WriteLine();
            Console.WriteLine("Hit enter to continue");
            Console.WriteLine();
            Console.ReadLine();
        }
    }
}
