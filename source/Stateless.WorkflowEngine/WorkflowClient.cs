using Stateless.WorkflowEngine.Commands;
using Stateless.WorkflowEngine.Exceptions;
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
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        IWorkflowStore WorkflowStore { get; set; }

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
        /// Gets the count of workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        long GetIncompleteCount();

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
        private readonly IWorkflowRegistrationService _workflowRegistrationService;

        public WorkflowClient(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowRegistrationService(), new CommandFactory())
        {
        }

        public WorkflowClient(IWorkflowStore workflowStore, IWorkflowRegistrationService workflowRegistrationService, ICommandFactory commandFactory)
        {
            this.WorkflowStore = workflowStore;
            _workflowRegistrationService = workflowRegistrationService;

            this.CommandFactory = commandFactory;
        }

        /// <summary>
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        public IWorkflowStore WorkflowStore { get; set; }

        public ICommandFactory CommandFactory { get; set; }

        /// <summary>
        /// Deletes a workflow from the underlying store.  This checks workflows in the active store 
        /// only, not in the underlying Completed collection.
        /// </summary>
        /// <param name="workflowId"></param>
        public void Delete(Guid workflowId)
        {
            this.WorkflowStore.Delete(workflowId);
        }

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public bool Exists(Guid workflowId)
        {
            Workflow workflow = this.WorkflowStore.Get(workflowId);
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
            return this.WorkflowStore.Get<T>(workflowId);
        }

        /// <summary>
        /// Gets the count of workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        public long GetIncompleteCount()
        {
            return this.WorkflowStore.GetIncompleteCount();
        }

        /// <summary>
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        public long GetCompletedCount()
        {
            return this.WorkflowStore.GetCompletedCount();
        }

        /// <summary>
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        public long GetSuspendedCount()
        {
            return this.WorkflowStore.GetSuspendedCount();
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow
        {
            return _workflowRegistrationService.IsSingleInstanceWorkflowRegistered<T>(this.WorkflowStore);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void Register(Workflow workflow)
        {
            _workflowRegistrationService.RegisterWorkflow(this.WorkflowStore, workflow);
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
            cmd.WorkflowStore = this.WorkflowStore;
            return cmd.Execute();
        }

    }
}
