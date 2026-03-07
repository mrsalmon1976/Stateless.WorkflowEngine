using Stateless.WorkflowEngine.Stores;
using System;
using System.Threading.Tasks;

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
        /// Deletes a workflow from the underlying store.  This checks workflows in the active store
        /// only, not in the underlying Completed collection.
        /// </summary>
        /// <param name="workflowId"></param>
        Task DeleteAsync(Guid workflowId);

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        bool Exists(Guid workflowId);

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Guid workflowId);

        /// <summary>
        /// Gets a workflow from the back-end store.  Returns null if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        T Get<T>(Guid workflowId) where T : Workflow;

        /// <summary>
        /// Gets a workflow from the back-end store.  Returns null if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        Task<T> GetAsync<T>(Guid workflowId) where T : Workflow;

        /// <summary>
        /// Gets the count of workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        long GetIncompleteCount();

        /// <summary>
        /// Gets the count of workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        Task<long> GetIncompleteCountAsync();

        /// <summary>
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        long GetCompletedCount();

        /// <summary>
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        Task<long> GetCompletedCountAsync();

        /// <summary>
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        long GetSuspendedCount();

        /// <summary>
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        Task<long> GetSuspendedCountAsync();

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
        void Register(Workflow workflow);

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        Task RegisterAsync(Workflow workflow);

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        Workflow Unsuspend(Guid workflowId);

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        Task<Workflow> UnsuspendAsync(Guid workflowId);

    }
}
