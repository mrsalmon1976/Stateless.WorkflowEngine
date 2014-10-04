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


        // Delete workflow
        // Get workflow
        // Exists
        // 
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
