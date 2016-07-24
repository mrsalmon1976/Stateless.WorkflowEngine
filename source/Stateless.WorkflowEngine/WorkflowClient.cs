using Stateless.WorkflowEngine.Commands;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowClient
    {

        /// <summary>
        /// Deletes a workflow from the underlying store.  This checks workflows in the active store 
        /// only, not in the underlying Completed collection.
        /// </summary>
        /// <param name="workflowId"></param>
        void Delete(Guid workflowId);

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        bool Exists(Guid workflowId);

        /// <summary>
        /// Gets a workflow from the back-end store.  Returns null if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        T Get<T>(Guid workflowId) where T : Workflow;

        /// <summary>
        /// Gets the count of active workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        long GetActiveCount();

        /// <summary>
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        long GetCompletedCount();

        /// <summary>
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        long GetSuspendedCount();

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
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        void Register(Workflow workflow);

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        [Obsolete("Use Register instead")]
        void RegisterWorkflow(Workflow workflow);

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        Workflow Unsuspend(Guid workflowId);

    }
    
    public class WorkflowClient : IWorkflowClient
    {
        private readonly IWorkflowStore _workflowStore;
        private readonly IWorkflowRegistrationService _workflowRegistrationService;

        public WorkflowClient(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowRegistrationService(), new CommandFactory())
        {
        }

        public WorkflowClient(IWorkflowStore workflowStore, IWorkflowRegistrationService workflowRegistrationService, ICommandFactory commandFactory)
        {
            _workflowStore = workflowStore;
            _workflowRegistrationService = workflowRegistrationService;

            this.CommandFactory = commandFactory;
        }

        public ICommandFactory CommandFactory { get; set; }

        /// <summary>
        /// Deletes a workflow from the underlying store.  This checks workflows in the active store 
        /// only, not in the underlying Completed collection.
        /// </summary>
        /// <param name="workflowId"></param>
        public void Delete(Guid workflowId)
        {
            _workflowStore.Delete(workflowId);
        }

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public bool Exists(Guid workflowId)
        {
            Workflow workflow = this._workflowStore.Get(workflowId);
            return (workflow != null);
        }

        /// <summary>
        /// Gets a workflow from the back-end store.  Returns null if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public T Get<T>(Guid workflowId) where T : Workflow
        {
            return _workflowStore.Get<T>(workflowId);
        }

        /// <summary>
        /// Gets the count of active workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        public long GetActiveCount()
        {
            return _workflowStore.GetActiveCount();
        }

        /// <summary>
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        public long GetCompletedCount()
        {
            return _workflowStore.GetCompletedCount();
        }

        /// <summary>
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        public long GetSuspendedCount()
        {
            return _workflowStore.GetSuspendedCount();
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
        public void Register(Workflow workflow)
        {
            _workflowRegistrationService.RegisterWorkflow(_workflowStore, workflow);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        [Obsolete("Use Register instead")]
        public void RegisterWorkflow(Workflow workflow)
        {
            this.Register(workflow);
        }

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public Workflow Unsuspend(Guid workflowId)
        {
            UnsuspendWorkflowCommand cmd = this.CommandFactory.CreateCommand<UnsuspendWorkflowCommand>();
            cmd.WorkflowId = workflowId;
            cmd.WorkflowStore = this._workflowStore;
            return cmd.Execute();
        }

    }
}
