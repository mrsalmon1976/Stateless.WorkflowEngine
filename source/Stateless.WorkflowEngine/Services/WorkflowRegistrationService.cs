using Stateless.WorkflowEngine.Exceptions;
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
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool IsSingleInstanceWorkflowRegistered<T>(IWorkflowStore workflowStore) where T : Workflow;

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
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>(IWorkflowStore workflowStore) where T : Workflow
        {
            IEnumerable<T> workflows = workflowStore.GetAllByType<T>();
            T wf = workflows.SingleOrDefault();
            if (wf == null) return false;
            if (!wf.IsSingleInstance) throw new WorkflowException(String.Format("A workflow of type {0} is registered, but not as a single instance workflow.", typeof(T).FullName));
            return true;
        }

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
                string qualifiedName = workflow.QualifiedName;
                IEnumerable<Workflow> workflows = workflowStore.GetAllByQualifiedName(qualifiedName);
                if (workflows.Any())
                {
                    throw new SingleInstanceWorkflowAlreadyExistsException(String.Format("Workflow of type '{0}' already registered", qualifiedName));
                }
            }

            // save the workflow, this can be handled now by the engine
            //workflow.Info.State = workflow.GetCurrentState();
            workflowStore.Save(workflow);
        }
    }
}
