using MongoDB.Driver;
using Stateless.TestHarness.Multithread;
using Stateless.TestHarness.Multithread.Workflow;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Stateless.TestHarness
{
    internal class MultithreadTestHarness
    {
        public static void Run()
        {
            const int TestRecordCount = 10000;
            var mongoClient = new MongoClient("mongodb://localhost");
            var database = mongoClient.GetDatabase(AppSettings.MongoDbDatabaseName);
            IWorkflowStore workflowStore = new MongoDbWorkflowStore(database);
            IWorkflowClient workflowClient = new WorkflowClient(workflowStore);
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflowType<MultithreadWorkflow>();

            MultithreadMigrator.Run();

            MultithreadRecordCreatorWorker creatorWorker = new MultithreadRecordCreatorWorker(workflowClient, TestRecordCount);
            creatorWorker.RunWorkerAsync();

            MultithreadRecordProcessorWorker processWorker = new MultithreadRecordProcessorWorker(workflowServer);
            processWorker.RunWorkerAsync();

        }
    }
}
