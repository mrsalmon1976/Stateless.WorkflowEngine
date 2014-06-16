using Stateless.WorkflowEngine;
using StructureMap;
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
            //Logger.Info<CPSBankingFTPService>("Service started");
        }

        protected override void OnStop()
        {
            _shutdownEvent.Set();

            // give the thread 3 seconds to stop
            if (!_thread.Join(3000))
            {
                _thread.Abort();
            }

            //Logger.Info<CPSBankingFTPService>("Service stopped");
        }

        void WorkerThreadFunc()
        {
            // invoke the workflow client and create a new example workflow if it hasn't been registered already
            IWorkflowClient workflowClient = ObjectFactory.GetInstance<IWorkflowClient>();
            if (!workflowClient.IsSingleInstanceWorkflowRegistered<FileCreationWorkflow>())
            {
                FileCreationWorkflow workflow = new FileCreationWorkflow(FileCreationWorkflow.State.Start);
                workflow.RootFolder = "C:\\Temp\\IUA.Workflow\\";
                workflow.ResumeTrigger = FileCreationWorkflow.Trigger.SendFirstEmail.ToString();
                workflowClient.RegisterWorkflow(workflow);
            }

            // fire up the server, this will run as part of the service
            IWorkflowServer workflowServer = ObjectFactory.GetInstance<IWorkflowServer>();

            while (!_shutdownEvent.WaitOne(0))
            {

                try
                {
                    workflowServer.ExecuteWorkflows(5);
                }
                catch (Exception)
                {
                    //Logger.Error<CPSBankingFTPService>(ex.Message, ex);
                    Thread.Sleep(1000);
                }

                // sleepy sleep
                Thread.Sleep(1000);
            }



        }
    }
}
