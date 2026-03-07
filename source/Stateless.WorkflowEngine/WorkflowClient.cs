using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine
{
    public class WorkflowClient : IWorkflowClient
    {
        private readonly IWorkflowRegistrationService _workflowRegistrationService;

        public WorkflowClient(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowRegistrationService())
        {
        }

        public WorkflowClient(IWorkflowStore workflowStore, IWorkflowRegistrationService workflowRegistrationService)
        {
            this.WorkflowStore = workflowStore;
            _workflowRegistrationService = workflowRegistrationService;
        }

        /// <summary>
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        public IWorkflowStore WorkflowStore { get; set; }

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
        /// Deletes a workflow from the underlying store.  This checks workflows in the active store
        /// only, not in the underlying Completed collection.
        /// </summary>
        /// <param name="workflowId"></param>
        public async Task DeleteAsync(Guid workflowId)
        {
            await this.WorkflowStore.DeleteAsync(workflowId);
        }

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public bool Exists(Guid workflowId)
        {
            Workflow workflow = this.WorkflowStore.GetOrDefault(workflowId);
            return (workflow != null);
        }

        /// <summary>
        /// Gets whether a workflow still exists or not.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Guid workflowId)
        {
            Workflow workflow = await this.WorkflowStore.GetOrDefaultAsync(workflowId);
            return (workflow != null);
        }

        /// <summary>
        /// Gets a workflow from the back-end store.  Throws a WorkflowNotFoundException if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public T Get<T>(Guid workflowId) where T : Workflow
        {
            return this.WorkflowStore.Get<T>(workflowId);
        }

        /// <summary>
        /// Gets a workflow from the back-end store.  Throws a WorkflowNotFoundException if the workflow does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public async Task<T> GetAsync<T>(Guid workflowId) where T : Workflow
        {
            Workflow workflow = await this.WorkflowStore.GetOrDefaultAsync(workflowId);
            if (workflow == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", workflowId));
            }
            return (T)workflow;
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
        /// Gets the count of workflows on the underlying store (including suspended).
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetIncompleteCountAsync()
        {
            return await this.WorkflowStore.GetIncompleteCountAsync();
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
        /// Gets the count of completed workflows on the underlying store.
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetCompletedCountAsync()
        {
            return await this.WorkflowStore.GetCompletedCountAsync();
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
        /// Gets the count of suspended workflows that have not completed on the underlying store.
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetSuspendedCountAsync()
        {
            return await this.WorkflowStore.GetSuspendedCountAsync();
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
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<bool> IsSingleInstanceWorkflowRegisteredAsync<T>() where T : Workflow
        {
            return await _workflowRegistrationService.IsSingleInstanceWorkflowRegisteredAsync<T>(this.WorkflowStore);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public void Register(Workflow workflow)
        {
            _workflowRegistrationService.RegisterWorkflow(this.WorkflowStore, workflow);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public async Task RegisterAsync(Workflow workflow)
        {
            await _workflowRegistrationService.RegisterWorkflowAsync(this.WorkflowStore, workflow);
        }

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public Workflow Unsuspend(Guid workflowId)
        {
            Workflow workflow = this.WorkflowStore.GetOrDefault(workflowId);

            if (workflow == null)
            {
                throw new WorkflowNotFoundException($"Workflow not found matching id {workflowId}");
            }

            workflow.IsSuspended = false;
            workflow.ResumeOn = DateTime.UtcNow;
            this.WorkflowStore.Save(workflow);

            return workflow;
        }

        /// <summary>
        /// Unsuspends a workflow.
        /// </summary>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public async Task<Workflow> UnsuspendAsync(Guid workflowId)
        {
            Workflow workflow = await this.WorkflowStore.GetOrDefaultAsync(workflowId);

            if (workflow == null)
            {
                throw new WorkflowNotFoundException($"Workflow not found matching id {workflowId}");
            }

            workflow.IsSuspended = false;
            workflow.ResumeOn = DateTime.UtcNow;
            await this.WorkflowStore.SaveAsync(workflow);

            return workflow;
        }

    }
}
