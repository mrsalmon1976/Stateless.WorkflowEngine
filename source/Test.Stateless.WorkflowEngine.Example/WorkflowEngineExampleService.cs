using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Example.Workflows.FileCreation;

namespace Test.Stateless.WorkflowEngine.Example
{
    partial class WorkflowEngineExampleService : ServiceBase
    {
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Thread _thread;

        public WorkflowEngineExampleService()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Debug method required to test service using infinite thread
        /// </summary>
        public void Start()
        {
            OnStart(new string[] { });
        }

        protected override void OnStart(string[] args)
        {
            _thread = new Thread(WorkerThreadFunc);
            _thread.Name = "WorkflowEngine Example Worker";
            _thread.IsBackground = true;
            _thread.Start();
        }

        protected override void OnStop()
        {
            _shutdownEvent.Set();

            // give the thread 3 seconds to stop
            if (!_thread.Join(3000))
            {
                _thread.Abort();
            }
        }

        void WorkerThreadFunc()
        {
            // fire up the server, this will run as part of the service (you can set the workflow store here)
            IWorkflowStore workflowStore = BootStrapper.MongoDbStore();
            //IWorkflowStore workflowStore = BootStrapper.MemoryStore();
            //IWorkflowStore workflowStore = BootStrapper.RavenDbEmbeddedStore();
            
            IWorkflowServer workflowServer = new WorkflowServer(workflowStore);
            workflowServer.RegisterWorkflowType<FileCreationWorkflow>();

            // create a new example workflow if it hasn't been registered already
            FileCreationWorkflow workflow = new FileCreationWorkflow(FileCreationWorkflow.State.Start);
            workflow.RootFolder = "C:\\Temp\\";
            workflow.ResumeTrigger = FileCreationWorkflow.Trigger.WriteFirstFile.ToString();
            workflow.Priority = 5;
            workflowServer.RegisterWorkflow(workflow);

            while (!_shutdownEvent.WaitOne(0))
            {
                int executedCount = 0;

                try
                {
                    executedCount = workflowServer.ExecuteWorkflows(5);
                }
                catch (Exception)
                {
                    // do some logging!
                }

                // if no workflows were found, sleepy sleep - you should create an app setting for the poll 
                // interval appropriate to you
                if (executedCount == 0)
                {
                    Thread.Sleep(1000);
                }
            }



        }
    }
}
