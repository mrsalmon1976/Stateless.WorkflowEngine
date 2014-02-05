using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System.Threading.Tasks;
using Stateless;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Services;
using StructureMap;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowServer
    {
        /// <summary>
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        void ExecuteWorkflow(Workflow workflow);

        /// <summary>
        /// Executes the first <c>count</c> workflows in the registered store, ordered by RetryCount, and then 
        /// by CreationDate.
        /// </summary>
        /// <param name="count"></param>
        void ExecuteWorkflows(int count);

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        void RegisterWorkflow(Workflow workflow);

    }

    public class WorkflowServer : IWorkflowServer
    {
        private readonly IWorkflowStore _workflowStore;

        public WorkflowServer(IWorkflowStore workflowStore)
        {
            _workflowStore = workflowStore;
        }

        /// <summary>
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public void ExecuteWorkflow(Workflow workflow)
        {
            bool exceptionRaised = false;

            try
            {
                workflow.LastException = null;
                workflow.RetryCount += 1;

                workflow.Fire(workflow.ResumeTrigger);
            }
            catch (Exception ex)
            {
                exceptionRaised = true;
                workflow.LastException = ex.ToString();

                // if an error occurred running the workflow, we need to set a resume trigger
                if (workflow.RetryIntervals.Length > workflow.RetryCount)
                {
                    workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryIntervals[workflow.RetryCount]);
                }
                else
                {
                    // we've run out of retry intervals, suspend the workflow
                    workflow.IsSuspended = true;
                }

            }

            // save the workflow, making sure the state is correct for the workflow
            _workflowStore.Save(workflow);

            // if the workflow is complete, finish off
            if (workflow.IsComplete)
            {
                _workflowStore.Archive(workflow);
                return;
            }

            // if the workflow is ready to resume immediately, just execute immediately instead of waiting for polling
            if (!String.IsNullOrWhiteSpace(workflow.ResumeTrigger) &&
                !exceptionRaised &&
                (!workflow.ResumeOn.HasValue || workflow.ResumeOn.Value <= DateTime.UtcNow))
            {
                this.ExecuteWorkflow(workflow);
            }
        }

        /// <summary>
        /// Executes the first <c>count</c> workflows in the registered store, ordered by RetryCount, and then 
        /// by CreationDate.
        /// </summary>
        /// <param name="count"></param>
        public void ExecuteWorkflows(int count)
        {
            IEnumerable<Workflow> workflows = _workflowStore.GetActive(count);
            Parallel.ForEach(workflows, ExecuteWorkflow);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void RegisterWorkflow(Workflow workflow)
        {
            IWorkflowRegistrationService regService = ObjectFactory.GetInstance<IWorkflowRegistrationService>();
            regService.RegisterWorkflow(_workflowStore, workflow);
        }

    }
}
