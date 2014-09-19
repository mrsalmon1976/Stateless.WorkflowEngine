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
        void RegisterWorkflow(Workflow workflow);
    }
    
    public class WorkflowClient : IWorkflowClient
    {
        private readonly IWorkflowStore _workflowStore;
        private readonly IWorkflowRegistrationService _workflowRegistrationService;

        public WorkflowClient(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowRegistrationService())
        {
        }

        public WorkflowClient(IWorkflowStore workflowStore, IWorkflowRegistrationService workflowRegistrationService)
        {
            _workflowStore = workflowStore;
            _workflowRegistrationService = workflowRegistrationService;
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
    }
}
