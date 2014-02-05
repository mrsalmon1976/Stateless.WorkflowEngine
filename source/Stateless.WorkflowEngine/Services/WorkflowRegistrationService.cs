using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Services
{
    public interface IWorkflowRegistrationService
    {
        /// <summary>
        /// Registers a new workflow.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        void RegisterWorkflow(IWorkflowStore workflowStore, Workflow workflow);
    }

    public class WorkflowRegistrationService : IWorkflowRegistrationService
    {
        /// <summary>
        /// Registers a new workflow.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void RegisterWorkflow(IWorkflowStore workflowStore, Workflow workflow)
        {
            if (workflow.IsSingleInstance)
            {
                string workflowType = workflow.GetType().AssemblyQualifiedName;
                IEnumerable<Workflow> workflows = workflowStore.GetAllByType(workflowType);
                if (workflows.Any())
                {
                    throw new SingleInstanceWorkflowAlreadyExistsException(String.Format("Workflow of type '{0}' already registered", workflowType));
                }
            }

            // save the workflow, this can be handled now by the engine
            //workflow.Info.State = workflow.GetCurrentState();
            workflowStore.Save(workflow);
        }
    }
}
