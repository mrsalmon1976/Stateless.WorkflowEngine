using Stateless.WorkflowEngine.Events;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Threading;
using System.Threading.Tasks;

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
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        Task ExecuteWorkflowAsync(Workflow workflow);

        /// <summary>
        /// Executes the first <c>count</c> workflows in the registered store, ordered by Priority DESC, RetryCount DESC, and then
        /// by CreationDate.  You can optionally elect to specify the number of workflows that should execute in parallel.
        /// </summary>
        /// <param name="count">The number of active workflows to be loaded for processing.</param>
        /// <param name="maxConcurrent">The maximum number of workflows to processing parallel - defaults to the value of <c>count</c>.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        int ExecuteWorkflows(int count, int? maxConcurrent = null);

        /// <summary>
        /// Executes the first <c>count</c> workflows in the registered store, ordered by Priority DESC, RetryCount DESC, and then
        /// by CreationDate.  You can optionally elect to specify the number of workflows that should execute in parallel.
        /// </summary>
        /// <param name="count">The number of active workflows to be loaded for processing.</param>
        /// <param name="maxConcurrent">The maximum number of workflows to processing parallel - defaults to the value of <c>count</c>.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        Task<int> ExecuteWorkflowsAsync(int count, int? maxConcurrent = null, CancellationToken? cancellationToken = null);

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow;

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<bool> IsSingleInstanceWorkflowRegisteredAsync<T>() where T : Workflow;

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        void RegisterWorkflow(Workflow workflow);

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        Task RegisterWorkflowAsync(Workflow workflow);

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
}
