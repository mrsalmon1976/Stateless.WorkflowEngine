using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System.Threading.Tasks;
using Stateless;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Events;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowServer
    {
        /// <summary>
        /// Gets/sets the resolver used to instantiate new instances of classes required for workflow execution.
        /// This defaults to null, in which case classes are created with reflection.  Setting this property 
        /// to your own resolver will allow you to control how workflow actions are created.
        /// </summary>
        IWorkflowEngineDependencyResolver DependencyResolver { get; set; }

        /// <summary>
        /// Gets/sets the options applicable to the workflow server.
        /// </summary>
        WorkflowServerOptions Options { get; set; }

        /// <summary>
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        IWorkflowStore WorkflowStore { get; set; }


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
        /// Gets the count of active (unsuspended) workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        long GetActiveCount();

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
        public WorkflowServer(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowServerOptions())
        {
        }

        public WorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions options)
        {
            this.WorkflowStore = workflowStore;
            this.Options = options ?? new WorkflowServerOptions();
            this.WorkflowRegistrationService = new WorkflowRegistrationService();
            this.WorkflowExceptionHandler = new WorkflowExceptionHandler();

            workflowStore.Initialise(this.Options.AutoCreateTables, this.Options.AutoCreateIndexes, this.Options.PersistWorkflowDefinitions);
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
        /// Gets/sets the workflow registration service
        /// </summary>
        internal IWorkflowRegistrationService WorkflowRegistrationService { get; set; }

        /// <summary>
        /// Gets/sets the workflow registration service
        /// </summary>
        internal IWorkflowExceptionHandler WorkflowExceptionHandler { get; set; }

        /// <summary>
        /// Gets/sets the resolver used to instantiate new instances of classes required for workflow execution.
        /// This defaults to null, in which case classes are created with reflection.  Setting this property 
        /// to your own resolver will allow you to control how workflow actions are created.
        /// </summary>
        public IWorkflowEngineDependencyResolver DependencyResolver { get; set; }

        /// <summary>
        /// Gets/sets the options applicable to the workflow server.
        /// </summary>
        public WorkflowServerOptions Options { get; set; }

        /// <summary>
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        public IWorkflowStore WorkflowStore { get; set; }

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

            // set the dependency resolver on the workflow to allow for dependency injection
            workflow.DependencyResolver = this.DependencyResolver;

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
                workflow.CurrentState = initialState;
                this.WorkflowExceptionHandler.HandleWorkflowException(workflow, ex);

                // raise the exception handler
                workflow.OnError(ex);

                // if the workflow is suspended, raise the events
                if (workflow.IsSuspended)
                {
                    workflow.OnSuspend();
                    if (this.WorkflowSuspended != null)
                    {
                        this.WorkflowSuspended(this, new WorkflowEventArgs(workflow));
                    }
                }

                // exit out, nothing else to do here
                return;
            }
            finally
            {
                // the workflow should always save, no matter what happens
                this.WorkflowStore.Save(workflow);
            }
            // if the workflow is complete, finish off
            if (workflow.IsComplete)
            {
                workflow.CompletedOn = DateTime.UtcNow;
                this.WorkflowStore.Archive(workflow);
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
            IEnumerable<Workflow> workflows = this.WorkflowStore.GetActive(count);
            int cnt = workflows.Count();
            Parallel.ForEach(workflows, ExecuteWorkflow);
            return cnt;
        }

        /// <summary>
        /// Gets the count of active (unsuspended) workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        public long GetActiveCount()
        {
            return this.WorkflowStore.GetActiveCount();
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow
        {
            return this.WorkflowRegistrationService.IsSingleInstanceWorkflowRegistered<T>(this.WorkflowStore);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in 
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void RegisterWorkflow(Workflow workflow)
        {
            this.WorkflowRegistrationService.RegisterWorkflow(this.WorkflowStore, workflow);
        }

        /// <summary>
        /// Registers a workflow type for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterWorkflowType<T>() where T : Workflow
        {
            this.WorkflowStore.RegisterType(typeof(T));

            if (this.Options.PersistWorkflowDefinitions)
            {
                try
                {
                    Type workflowType = typeof(T);
                    Workflow workflow = Activator.CreateInstance(workflowType) as Workflow;
                    string graph = workflow.GetGraph();

                    // check to see if the definition already exists 
                    WorkflowDefinition workflowDefinition = this.WorkflowStore.GetDefinitionByQualifiedName(workflowType.FullName);
                    if (workflowDefinition == null)
                    {
                        workflowDefinition = new WorkflowDefinition();
                    }
                    workflowDefinition.Name = workflowType.Name;
                    workflowDefinition.QualifiedName = workflowType.FullName;
                    workflowDefinition.Graph = graph;
                    workflowDefinition.LastUpdatedUtc = DateTime.UtcNow;
                    this.WorkflowStore.SaveDefinition(workflowDefinition);
                }
                catch (Exception ex)
                {
                    throw new WorkflowException("Unable to generate workflow definition graph - ensure the WorkflowDefinitions configuration is correct or remove definitions from server options", ex);
                }
            }
        }

    }
}
