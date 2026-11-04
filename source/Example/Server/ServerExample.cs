using Example.Shared;
using Example.Shared.Workflows.FileCreation;
using Microsoft.Extensions.DependencyInjection;
using Stateless.WorkflowEngine;

namespace Example.Server
{
    internal class ServerExample
    {

        private static ServiceProvider CreateServiceProvider(string storeType)
        {
            ExampleServices.WriteStoreInfo(storeType, Constants.ExampleDbName);

            // the server logs at Information, so you can see what it does: workflows completing and retrying, and the
            // node starting and stopping
            return ExampleServices.Create(storeType, Constants.ExampleDbName, engine => engine
                // register every workflow type the server executes
                .AddWorkflowType<FileCreationWorkflow>()
                // register the workflow's actions, so their dependencies are checked when the provider is built
                .AddWorkflowActionsFromAssemblyOf<FileCreationWorkflow>(ExampleServices.InNamespaceOf<FileCreationWorkflow>));
        }

        public static void Run()
        {
            string inputStoreType = Prompts.GetInputStoreType();
            if (string.IsNullOrEmpty(inputStoreType))
                return;

            Console.WriteLine();

            // disposing the provider disposes the server, which releases its locks
            using ServiceProvider serviceProvider = CreateServiceProvider(inputStoreType);
            IWorkflowServer workflowServer = serviceProvider.GetRequiredService<IWorkflowServer>();

            Console.WriteLine("Press enter when you are ready to start");
            Console.ReadLine();


            long activeCount = workflowServer.WorkflowStore.GetActiveCount();
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
                    return;
                }

                if (executedCount == 0)
                {
                    Console.WriteLine("No more workflows found to execute - sleeping for 1 second");
                    Thread.Sleep(1000);
                }

                if (workflowServer.WorkflowStore.GetActiveCount() == 0)
                {
                    Console.WriteLine("No more active workflows, exiting worker loop");
                    break;
                }
            }

            Console.WriteLine("All workflows have completed or suspended");
            Console.WriteLine("Hit enter to continue");
            Console.ReadLine();
        }
    }
}
