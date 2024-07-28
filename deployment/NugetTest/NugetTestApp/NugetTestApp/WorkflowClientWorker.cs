using NugetTestApp.Workflow;
using Stateless.WorkflowEngine;
using System.ComponentModel;

namespace NugetTestApp
{
    internal class WorkflowClientWorker : BackgroundWorker
    {
        private readonly IWorkflowClient _workflowClient;
        private int _recordCount;

        public WorkflowClientWorker(IWorkflowClient workflowClient, int recordCount)
        {
            this._workflowClient = workflowClient;
            this._recordCount = recordCount;
            this.DoWork += WorkflowCreationWorker_DoWork;
        }

        private void WorkflowCreationWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            Random r = new Random();

            for (int i = 1; i < (_recordCount + 1); i++)
            {
                TestWorkflow workflow = new TestWorkflow();
                workflow.WorkflowNumber = i;
                _workflowClient.Register(workflow);
                ConsoleWriter.WriteLine("CREATION: ", $"Workflow {i} created", ConsoleColor.Green);

                int delay = r.Next(10, 200);
                Thread.Sleep(delay);
            }
        }

       
    }
}
