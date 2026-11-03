using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Represents a store that is used to persist or retrieve persisted workflows.
    /// </summary>
    public interface IWorkflowStore
    {
        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        void Archive(Workflow workflow);

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        Task ArchiveAsync(Workflow workflow);

        /// <summary>
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id"></param>
        void Delete(Guid id);

        /// <summary>
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id"></param>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Workflow Get(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        T Get<T>(Guid id) where T : Workflow;

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Workflow> GetAsync(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<T> GetAsync<T>(Guid id) where T : Workflow;

        /// <summary>
        /// Gets the count of unsuspended workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        long GetActiveCount();

        /// <summary>
        /// Gets the count of unsuspended workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        Task<long> GetActiveCountAsync();

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName);

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        Task<WorkflowDefinition> GetDefinitionByQualifiedNameAsync(string qualifiedName);

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        IEnumerable<WorkflowDefinition> GetDefinitions();

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<WorkflowDefinition>> GetDefinitionsAsync();

        /// <summary>
        /// Gets the count of workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        long GetIncompleteCount();

        /// <summary>
        /// Gets the count of workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        Task<long> GetIncompleteCountAsync();

		/// <summary>
		/// Gets all workflows of a specified fully qualified name.
		/// </summary>
		/// <returns></returns>
		IEnumerable<T> GetAllByQualifiedName<T>() where T : Workflow;

		/// <summary>
		/// Gets all workflows of a specified fully qualified name.
		/// </summary>
		/// <returns></returns>
		IEnumerable<Workflow> GetAllByQualifiedName(string qualifiedName);

		/// <summary>
		/// Gets all workflows of a specified fully qualified name.
		/// </summary>
		/// <returns></returns>
		Task<IEnumerable<T>> GetAllByQualifiedNameAsync<T>() where T : Workflow;

		/// <summary>
		/// Gets all workflows of a specified fully qualified name.
		/// </summary>
		/// <returns></returns>
		Task<IEnumerable<Workflow>> GetAllByQualifiedNameAsync(string qualifiedName);

		/// <summary>
		/// Gets all workflows of a specified type.
		/// </summary>
		/// <returns></returns>
		IEnumerable<T> GetAllByType<T>() where T : Workflow;

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Workflow> GetAllByType(string workflowType);

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<T>> GetAllByTypeAsync<T>() where T : Workflow;

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<Workflow>> GetAllByTypeAsync(string workflowType);

        /// <summary>
        /// Gets a completed workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Workflow GetCompleted(Guid id);

        /// <summary>
        /// Gets a completed workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Workflow> GetCompletedAsync(Guid id);

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        long GetCompletedCount();

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        Task<long> GetCompletedCountAsync();

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Workflow GetCompletedOrDefault(Guid id);

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Workflow> GetCompletedOrDefaultAsync(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        Workflow GetOrDefault(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<Workflow> GetOrDefaultAsync(Guid id);


        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IEnumerable<Workflow> GetActive(int count);

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<IEnumerable<Workflow>> GetActiveAsync(int count);

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IEnumerable<Workflow> GetIncomplete(int count);

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<IEnumerable<Workflow>> GetIncompleteAsync(int count);

        /// <summary>
        /// Gets all incomplete workflows as JSON documents.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IEnumerable<string> GetIncompleteWorkflowsAsJson(int count);

        /// <summary>
        /// Gets all incomplete workflows as JSON documents.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<IEnumerable<string>> GetIncompleteWorkflowsAsJsonAsync(int count);

        /// <summary>
        /// Gets the json version of a workflow.
        /// </summary>
        /// <param name="connectionModel"></param>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        string GetWorkflowAsJson(Guid id);

        /// <summary>
        /// Gets the json version of a workflow.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<string> GetWorkflowAsJsonAsync(Guid id);

        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        long GetSuspendedCount();

        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        Task<long> GetSuspendedCountAsync();

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions);

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        Task InitialiseAsync(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions);


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        void RegisterType(Type t);

        /// <summary>
        /// Saves an existing workflow to the persistence store.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        void Save(Workflow workflow);

        /// <summary>
        /// Saves an existing workflow to the persistence store.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        Task SaveAsync(Workflow workflow);

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        void Save(IEnumerable<Workflow> workflows);

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        Task SaveAsync(IEnumerable<Workflow> workflows);

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        void SaveDefinition(WorkflowDefinition workflowDefinition);

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        Task SaveDefinitionAsync(WorkflowDefinition workflowDefinition);

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        void SuspendWorkflow(Guid id);

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        Task SuspendWorkflowAsync(Guid id);

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        void UnsuspendWorkflow(Guid id);

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        Task UnsuspendWorkflowAsync(Guid id);

        /// <summary>
        /// Atomically claims the next available workflow (not suspended, due for resumption and not locked), ordered by
        /// Priority DESC, RetryCount DESC and then CreatedOn, by setting <c>LockedBy</c> to the supplied node id.  This is
        /// the method used by the workflow server to fetch workflows - no two nodes can claim the same workflow.
        /// </summary>
        /// <param name="nodeId">The id of the node claiming the workflow.</param>
        /// <returns>The claimed workflow, or null if no workflows are available.</returns>
        Workflow ClaimNext(string nodeId);

        /// <summary>
        /// Atomically claims the next available workflow (not suspended, due for resumption and not locked), ordered by
        /// Priority DESC, RetryCount DESC and then CreatedOn, by setting <c>LockedBy</c> to the supplied node id.  This is
        /// the method used by the workflow server to fetch workflows - no two nodes can claim the same workflow.
        /// </summary>
        /// <param name="nodeId">The id of the node claiming the workflow.</param>
        /// <returns>The claimed workflow, or null if no workflows are available.</returns>
        Task<Workflow> ClaimNextAsync(string nodeId);

        /// <summary>
        /// Atomically claims up to <c>count</c> of the next available workflows (not suspended, due for resumption and
        /// not locked), ordered by Priority DESC, RetryCount DESC and then CreatedOn, by setting <c>LockedBy</c> to the
        /// supplied node id.  This is the method used by the workflow server to fetch workflows - each workflow is
        /// claimed atomically, so no two nodes can claim the same workflow.
        /// </summary>
        /// <param name="nodeId">The id of the node claiming the workflows.</param>
        /// <param name="count">The maximum number of workflows to claim.</param>
        /// <returns>The claimed workflows in claim order - fewer than <c>count</c> (or none) if no more are available.</returns>
        IEnumerable<Workflow> ClaimNext(string nodeId, int count);

        /// <summary>
        /// Atomically claims up to <c>count</c> of the next available workflows (not suspended, due for resumption and
        /// not locked), ordered by Priority DESC, RetryCount DESC and then CreatedOn, by setting <c>LockedBy</c> to the
        /// supplied node id.  This is the method used by the workflow server to fetch workflows - each workflow is
        /// claimed atomically, so no two nodes can claim the same workflow.
        /// </summary>
        /// <param name="nodeId">The id of the node claiming the workflows.</param>
        /// <param name="count">The maximum number of workflows to claim.</param>
        /// <returns>The claimed workflows in claim order - fewer than <c>count</c> (or none) if no more are available.</returns>
        Task<IEnumerable<Workflow>> ClaimNextAsync(string nodeId, int count);

        /// <summary>
        /// Saves a workflow claimed by a node.  The save only happens if the stored workflow is still locked by the node,
        /// or is not locked at all.  The <c>LockedBy</c> value of the workflow is persisted, so clearing it before the
        /// save releases the lock in the same write.
        /// </summary>
        /// <returns>False if the workflow is now locked by another node (or no longer exists), in which case nothing is saved.</returns>
        bool SaveClaimed(Workflow workflow, string nodeId);

        /// <summary>
        /// Saves a workflow claimed by a node.  The save only happens if the stored workflow is still locked by the node,
        /// or is not locked at all.  The <c>LockedBy</c> value of the workflow is persisted, so clearing it before the
        /// save releases the lock in the same write.
        /// </summary>
        /// <returns>False if the workflow is now locked by another node (or no longer exists), in which case nothing is saved.</returns>
        Task<bool> SaveClaimedAsync(Workflow workflow, string nodeId);

        /// <summary>
        /// Archives a workflow claimed by a node, moving it into the completed store.  The archive only happens if the
        /// stored workflow is still locked by the node, or is not locked at all.
        /// </summary>
        /// <returns>False if the workflow is now locked by another node (or no longer exists), in which case nothing is archived.</returns>
        bool ArchiveClaimed(Workflow workflow, string nodeId);

        /// <summary>
        /// Archives a workflow claimed by a node, moving it into the completed store.  The archive only happens if the
        /// stored workflow is still locked by the node, or is not locked at all.
        /// </summary>
        /// <returns>False if the workflow is now locked by another node (or no longer exists), in which case nothing is archived.</returns>
        Task<bool> ArchiveClaimedAsync(Workflow workflow, string nodeId);

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        void ReleaseLocks(string nodeId);

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        Task ReleaseLocksAsync(string nodeId);

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.  Used by
        /// a node to release locks it holds on workflows it is no longer executing (e.g. after a failed save).
        /// </summary>
        void ReleaseStaleLocks(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds);

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.  Used by
        /// a node to release locks it holds on workflows it is no longer executing (e.g. after a failed save).
        /// </summary>
        Task ReleaseStaleLocksAsync(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds);

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        IEnumerable<string> GetLockOwners();

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        Task<IEnumerable<string>> GetLockOwnersAsync();

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.
        /// </summary>
        void RegisterNode(WorkflowNode node);

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.
        /// </summary>
        Task RegisterNodeAsync(WorkflowNode node);

        /// <summary>
        /// Updates the heartbeat of a node.  The node is not re-created if it no longer exists.
        /// </summary>
        /// <returns>False if the node no longer exists (it was removed by another node).</returns>
        bool Heartbeat(string nodeId);

        /// <summary>
        /// Updates the heartbeat of a node.  The node is not re-created if it no longer exists.
        /// </summary>
        /// <returns>False if the node no longer exists (it was removed by another node).</returns>
        Task<bool> HeartbeatAsync(string nodeId);

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        IEnumerable<WorkflowNode> GetNodes();

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        Task<IEnumerable<WorkflowNode>> GetNodesAsync();

        /// <summary>
        /// Removes a node.
        /// </summary>
        void RemoveNode(string nodeId);

        /// <summary>
        /// Removes a node.
        /// </summary>
        Task RemoveNodeAsync(string nodeId);

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.
        /// </summary>
        /// <returns>The number of nodes removed.</returns>
        long RemoveStaleNodes(TimeSpan timeout);

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.
        /// </summary>
        /// <returns>The number of nodes removed.</returns>
        Task<long> RemoveStaleNodesAsync(TimeSpan timeout);
    }
}
