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
using NLog;

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

    }

    public class WorkflowServer : IWorkflowServer
    {
        private readonly IWorkflowStore _workflowStore;
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
            logger.Info("Executing workflow {0}", workflow.Id);
            string initialState = workflow.CurrentState;
            try
            {
                workflow.LastException = null;
                workflow.RetryCount += 1;
                logger.Info("Firing trigger {0} for workflow {1} (Type: {2}, Current state: {3})", workflow.ResumeTrigger, workflow.Id, workflow.GetType().FullName, workflow.CurrentState);
                workflow.Fire(workflow.ResumeTrigger);

                workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);
                IWorkflowExceptionHandler workflowExceptionHandler = ObjectFactory.GetInstance<IWorkflowExceptionHandler>();
                //workflow.IsSingleInstance ? workflowExceptionHandler.

                if (workflow.IsSingleInstance)
                {
                    workflowExceptionHandler.HandleSingleInstanceWorkflowException(workflow, ex);
                }
                else
                {
                    workflowExceptionHandler.HandleMultipleInstanceWorkflowException(workflow, ex);
                }
                workflow.CurrentState = initialState;

                // exit out, nothing else to do here
                return;
            }
            finally
            {
                // the workflow should always save, no matter what happens
                logger.Info("Persisting workflow {0} with store {1}", workflow.Id, _workflowStore.GetType().FullName);
                _workflowStore.Save(workflow);
            }
            // if the workflow is complete, finish off
            if (workflow.IsComplete)
            {
                logger.Info("Archiving workflow {0}", workflow.Id);
                _workflowStore.Archive(workflow);
                return;
            }

            // if the workflow is ready to resume immediately, just execute immediately instead of waiting for polling
            if (!String.IsNullOrWhiteSpace(workflow.ResumeTrigger) && workflow.ResumeOn <= DateTime.UtcNow)
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
            logger.Info("Retrieved {0} workflows for execution from the data store", workflows.Count());
            Parallel.ForEach(workflows, ExecuteWorkflow);
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow
        {
            IWorkflowRegistrationService regService = ObjectFactory.GetInstance<IWorkflowRegistrationService>();
            return regService.IsSingleInstanceWorkflowRegistered<T>(_workflowStore);
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
            logger.Info("Registering workflow {0}", workflow.GetType().FullName);
            regService.RegisterWorkflow(_workflowStore, workflow);
        }

        /// <summary>
        /// Registers a workflow type for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterWorkflowType<T>() where T : Workflow
        {
            IWorkflowStore store = ObjectFactory.GetInstance<IWorkflowStore>();
            if (store is MongoDbWorkflowStore)
            {
                Type t = typeof(T);
                logger.Info("Registering workflow type {0}", t.FullName);
                MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(t);
            }
        }

    }
}
