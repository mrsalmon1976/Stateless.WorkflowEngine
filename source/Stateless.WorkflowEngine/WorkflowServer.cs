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
using Stateless.WorkflowEngine.Events;

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
        /// <param name="count">The number of workflows that can be executed.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        int ExecuteWorkflows(int count);

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow;

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        void RegisterWorkflow(Workflow workflow);

        /// <summary>
        /// Registers a workflow type for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        void RegisterWorkflowType<T>() where T : Workflow;

        /// <summary>
        /// Event raised when a workflow is suspended.
        /// </summary>
        event EventHandler<WorkflowEventArgs> WorkflowSuspended;

        /// <summary>
        /// Event raised when a workflow completes.
        /// </summary>
        event EventHandler<WorkflowEventArgs> WorkflowCompleted;

    }

    public class WorkflowServer : IWorkflowServer
    {
        private readonly IWorkflowStore _workflowStore;
        private readonly IWorkflowRegistrationService _workflowRegistrationService;
        private readonly IWorkflowExceptionHandler _exceptionHandler;

        public WorkflowServer(IWorkflowStore workflowStore): this(workflowStore, new WorkflowRegistrationService(), new WorkflowExceptionHandler())
        {

        }

        public WorkflowServer(IWorkflowStore workflowStore, IWorkflowRegistrationService workflowRegistrationService, IWorkflowExceptionHandler exceptionHandler)
        {
            _workflowStore = workflowStore;
            _workflowRegistrationService = workflowRegistrationService;
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Event raised when a workflow is suspended.
        /// </summary>
        public event EventHandler<WorkflowEventArgs> WorkflowSuspended;

        /// <summary>
        /// Event raised when a workflow completes.
        /// </summary>
        public event EventHandler<WorkflowEventArgs> WorkflowCompleted;

        /// <summary>
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public void ExecuteWorkflow(Workflow workflow)
        {
            if (workflow == null)
            {
                throw new NullReferenceException("Workflow server asked to execute null workflow object");
            }

            string initialState = workflow.CurrentState;
            try
            {
                workflow.LastException = null;
                workflow.RetryCount += 1;
                workflow.Fire(workflow.ResumeTrigger);

                workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
            }
            catch (Exception ex)
            {
                if (workflow.IsSingleInstance)
                {
                    _exceptionHandler.HandleSingleInstanceWorkflowException(workflow, ex);
                }
                else
                {
                    _exceptionHandler.HandleMultipleInstanceWorkflowException(workflow, ex);
                }
                workflow.CurrentState = initialState;

                // raise the exception handler
                workflow.OnError(ex);

                // if the workflow is suspended, raise the events
                if (workflow.IsSuspended && this.WorkflowSuspended != null)
                {
                    this.WorkflowSuspended(this, new WorkflowEventArgs(workflow));
                }

                // exit out, nothing else to do here
                return;
            }
            finally
            {
                // the workflow should always save, no matter what happens
                _workflowStore.Save(workflow);
            }
            // if the workflow is complete, finish off
            if (workflow.IsComplete)
            {
                _workflowStore.Archive(workflow);
                workflow.OnComplete();
                if (this.WorkflowCompleted != null)
                {
                    this.WorkflowCompleted(this, new WorkflowEventArgs(workflow));
                }
                return;
            }

            // if the workflow is ready to resume immediately, just execute immediately instead of waiting for polling
            if (!workflow.IsSuspended && !String.IsNullOrWhiteSpace(workflow.ResumeTrigger) && workflow.ResumeOn <= DateTime.UtcNow)
            {
                this.ExecuteWorkflow(workflow);
            }
        }

        /// <summary>
        /// Executes the first <c>count</c> workflows in the registered store, ordered by RetryCount, and then 
        /// by CreationDate.
        /// </summary>
        /// <param name="count">The number of workflows that can be executed.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        public int ExecuteWorkflows(int count)
        {
            IEnumerable<Workflow> workflows = _workflowStore.GetActive(count);
            int cnt = workflows.Count();
            Parallel.ForEach(workflows, ExecuteWorkflow);
            return cnt;
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow
        {
            return _workflowRegistrationService.IsSingleInstanceWorkflowRegistered<T>(_workflowStore);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void RegisterWorkflow(Workflow workflow)
        {
            _workflowRegistrationService.RegisterWorkflow(_workflowStore, workflow);
        }

        /// <summary>
        /// Registers a workflow type for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterWorkflowType<T>() where T : Workflow
        {
            this._workflowStore.RegisterType(typeof(T));
        }

    }
}
