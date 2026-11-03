using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Stores workflows in memory only.  This should be used only in systems where 
    /// workflows do not need to be persisted and can be lost on system shutdown.
    /// </summary>
    public class MemoryWorkflowStore : WorkflowStore
    {
        private readonly Dictionary<Guid, Workflow> _activeWorkflows = new Dictionary<Guid, Workflow>();
        private readonly Dictionary<Guid, Workflow> _completedWorkflows = new Dictionary<Guid, Workflow>();
        private readonly List<WorkflowDefinition> _workflowDefinitions = new List<WorkflowDefinition>();
        private readonly Dictionary<string, WorkflowNode> _nodes = new Dictionary<string, WorkflowNode>();

        // the workflow objects are shared with the callers, so locks (owner and claim time) are tracked separately from
        // the Workflow.LockedBy/LockedOn properties, which an executing workflow server clears before saving - if that
        // save then fails, the lock must still be released as stale
        private readonly Dictionary<Guid, StoreLock> _locks = new Dictionary<Guid, StoreLock>();
        private readonly object syncLock = new object();

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            lock (syncLock)
            {
                _activeWorkflows.Remove(workflow.Id);
                _locks.Remove(workflow.Id);
                _completedWorkflows.Add(workflow.Id, workflow);
            }
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override async Task ArchiveAsync(Workflow workflow)
        {
            await Task.FromResult(0);
            Archive(workflow);
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override void Delete(Guid id)
        {
            lock (syncLock)
            {
                _activeWorkflows.Remove(id);
                _locks.Remove(id);
            }
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override async Task DeleteAsync(Guid id)
        {
            await Task.FromResult(0);
            Delete(id);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetActiveCount()
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values.Count(x => !x.IsSuspended);
            }
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetActiveCountAsync()
        {
            return await Task.FromResult(GetActiveCount());
        }

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName)
        {
            lock (syncLock)
            {
                return _workflowDefinitions.SingleOrDefault(x => x.QualifiedName == qualifiedName);
            }
        }

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override async Task<WorkflowDefinition> GetDefinitionByQualifiedNameAsync(string qualifiedName)
        {
            return await Task.FromResult(GetDefinitionByQualifiedName(qualifiedName));
        }

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<WorkflowDefinition> GetDefinitions()
        {
            lock (syncLock)
            {
                return _workflowDefinitions.ToList();
            }
        }

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<WorkflowDefinition>> GetDefinitionsAsync()
        {
            return await Task.FromResult(GetDefinitions());
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            lock (syncLock)
            {
                return _activeWorkflows.Count;
            }
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetIncompleteCountAsync()
        {
            return await Task.FromResult(GetIncompleteCount());
        }

		/// <summary>
		/// Gets all workflows of a specified fully qualified name ordered by create date.
		/// </summary>
		/// <returns></returns>
		public override IEnumerable<Workflow> GetAllByQualifiedName(string qualifiedName)
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values
                    .Where(x => x.GetType().FullName == qualifiedName)
                    .OrderBy(x => x.CreatedOn)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all workflows of a specified fully qualified name ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByQualifiedNameAsync(string qualifiedName)
        {
            return await Task.FromResult(GetAllByQualifiedName(qualifiedName));
        }

        /// <summary>
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values
                    .Where(x => x.GetType().AssemblyQualifiedName == workflowType)
                    .OrderBy(x => x.CreatedOn)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByTypeAsync(string workflowType)
        {
            return await Task.FromResult(GetAllByType(workflowType));
        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override long GetCompletedCount()
        {
            lock (syncLock)
            {
                return _completedWorkflows.Count;
            }
        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetCompletedCountAsync()
        {
            return await Task.FromResult(GetCompletedCount());
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetCompletedOrDefault(Guid id)
        {
            lock (syncLock)
            {
                _completedWorkflows.TryGetValue(id, out Workflow workflow);
                return workflow;
            }
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetCompletedOrDefaultAsync(Guid id)
        {
            return await Task.FromResult(GetCompletedOrDefault(id));
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetOrDefault(Guid id)
        {
            lock (syncLock)
            {
                _activeWorkflows.TryGetValue(id, out Workflow workflow);
                return workflow;
            }
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetOrDefaultAsync(Guid id)
        {
            return await Task.FromResult(GetOrDefault(id));
        }

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values
                    .Where(x => !x.IsSuspended && x.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Priority)
                    .ThenByDescending(x => x.RetryCount)
                    .ThenBy(x => x.CreatedOn)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetActiveAsync(int count)
        {
            return await Task.FromResult(this.GetActive(count));
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values
                    .Where(x => x.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Priority)
                    .ThenByDescending(x => x.RetryCount)
                    .ThenBy(x => x.CreatedOn)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetIncompleteAsync(int count)
        {
            return await Task.FromResult(GetIncomplete(count));
        }


        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override long GetSuspendedCount()
        {
            lock (syncLock)
            {
                return _activeWorkflows.Values.Count(x => x.IsSuspended);
            }
        }

        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetSuspendedCountAsync()
        {
            return await Task.FromResult(GetSuspendedCount());
        }

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            // does nothing - tables are in-memory and there are no indexes
        }

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override Task InitialiseAsync(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            // does nothing - tables are in-memory and there are no indexes
            return Task.CompletedTask;
        }


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        public override void RegisterType(Type t)
        {
            // no registration needed
        }

        /// <summary>
        /// Stores a new workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public override void Save(Workflow workflow)
        {
            lock (syncLock)
            {
                if (_activeWorkflows.ContainsKey(workflow.Id))
                {
                    _activeWorkflows[workflow.Id] = workflow;
                }
                else
                {
                    _activeWorkflows.Add(workflow.Id, workflow);
                }
                SetLock(workflow);
            }
        }

        /// <summary>
        /// Stores a new workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public override async Task SaveAsync(Workflow workflow)
        {
            await Task.FromResult(0);
            Save(workflow);
        }

        /// <summary>
        /// Stores a collection of new workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override void Save(IEnumerable<Workflow> workflows)
        {
            foreach (Workflow w in workflows)
            {
                Save(w);
            }
        }

        /// <summary>
        /// Stores a collection of new workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override async Task SaveAsync(IEnumerable<Workflow> workflows)
        {
            await Task.FromResult(0);
            Save(workflows);
        }

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override void SaveDefinition(WorkflowDefinition workflowDefinition)
        {
            lock (syncLock)
            {
                var existingDefinition = _workflowDefinitions.SingleOrDefault(x => x.QualifiedName == workflowDefinition.QualifiedName);
                if (existingDefinition != null)
                {
                    this._workflowDefinitions.Remove(existingDefinition);
                }
                this._workflowDefinitions.Add(workflowDefinition);
            }
        }

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override async Task SaveDefinitionAsync(WorkflowDefinition workflowDefinition)
        {
            await Task.FromResult(0);
            SaveDefinition(workflowDefinition);
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            lock (syncLock)
            {
                Workflow w = this.Get(id);
                w.IsSuspended = true;
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override async Task SuspendWorkflowAsync(Guid id)
        {
            await Task.FromResult(0);
            SuspendWorkflow(id);
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            lock (syncLock)
            {
                Workflow w = this.Get(id);
                w.IsSuspended = false;
                w.RetryCount = 0;
                w.ResumeOn = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override async Task UnsuspendWorkflowAsync(Guid id)
        {
            await Task.FromResult(0);
            UnsuspendWorkflow(id);
        }

        /// <summary>
        /// Atomically claims the next available workflow for a node.
        /// </summary>
        public override Workflow ClaimNext(string nodeId)
        {
            return ClaimNext(nodeId, 1).FirstOrDefault();
        }

        /// <summary>
        /// Atomically claims the next available workflow for a node.
        /// </summary>
        public override async Task<Workflow> ClaimNextAsync(string nodeId)
        {
            return await Task.FromResult(ClaimNext(nodeId));
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> of the next available workflows for a node.
        /// </summary>
        public override IEnumerable<Workflow> ClaimNext(string nodeId, int count)
        {
            lock (syncLock)
            {
                DateTime now = DateTime.UtcNow;
                List<Workflow> workflows = _activeWorkflows.Values
                    .Where(x => !x.IsSuspended && x.ResumeOn <= now && !_locks.ContainsKey(x.Id))
                    .OrderByDescending(x => x.Priority)
                    .ThenByDescending(x => x.RetryCount)
                    .ThenBy(x => x.CreatedOn)
                    .Take(count)
                    .ToList();
                foreach (Workflow workflow in workflows)
                {
                    workflow.LockedBy = nodeId;
                    workflow.LockedOn = now;
                    _locks[workflow.Id] = new StoreLock(nodeId, now);
                }
                return workflows;
            }
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> of the next available workflows for a node.
        /// </summary>
        public override async Task<IEnumerable<Workflow>> ClaimNextAsync(string nodeId, int count)
        {
            return await Task.FromResult(ClaimNext(nodeId, count));
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override bool SaveClaimed(Workflow workflow, string nodeId)
        {
            lock (syncLock)
            {
                if (!IsClaimable(workflow.Id, nodeId))
                {
                    return false;
                }
                _activeWorkflows[workflow.Id] = workflow;
                SetLock(workflow);
                return true;
            }
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override async Task<bool> SaveClaimedAsync(Workflow workflow, string nodeId)
        {
            return await Task.FromResult(SaveClaimed(workflow, nodeId));
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override bool ArchiveClaimed(Workflow workflow, string nodeId)
        {
            lock (syncLock)
            {
                if (!IsClaimable(workflow.Id, nodeId))
                {
                    return false;
                }
                Archive(workflow);
                return true;
            }
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override async Task<bool> ArchiveClaimedAsync(Workflow workflow, string nodeId)
        {
            return await Task.FromResult(ArchiveClaimed(workflow, nodeId));
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override void ReleaseLocks(string nodeId)
        {
            lock (syncLock)
            {
                Release(_locks.Where(x => x.Value.NodeId == nodeId).Select(x => x.Key).ToList());
            }
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override async Task ReleaseLocksAsync(string nodeId)
        {
            await Task.FromResult(0);
            ReleaseLocks(nodeId);
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override void ReleaseStaleLocks(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            lock (syncLock)
            {
                HashSet<Guid> exclude = new HashSet<Guid>(excludeIds);
                Release(_locks
                    .Where(x => x.Value.NodeId == nodeId && x.Value.LockedOn < lockedBefore && !exclude.Contains(x.Key))
                    .Select(x => x.Key)
                    .ToList());
            }
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override async Task ReleaseStaleLocksAsync(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            await Task.FromResult(0);
            ReleaseStaleLocks(nodeId, lockedBefore, excludeIds);
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        public override IEnumerable<string> GetLockOwners()
        {
            lock (syncLock)
            {
                return _locks.Values.Select(x => x.NodeId).Distinct().ToList();
            }
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        public override async Task<IEnumerable<string>> GetLockOwnersAsync()
        {
            return await Task.FromResult(GetLockOwners());
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.
        /// </summary>
        public override void RegisterNode(WorkflowNode node)
        {
            lock (syncLock)
            {
                node.LastHeartbeat = DateTime.UtcNow;
                _nodes[node.Id] = node;
            }
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.
        /// </summary>
        public override async Task RegisterNodeAsync(WorkflowNode node)
        {
            await Task.FromResult(0);
            RegisterNode(node);
        }

        /// <summary>
        /// Updates the heartbeat of a node, returning false if the node no longer exists.
        /// </summary>
        public override bool Heartbeat(string nodeId)
        {
            lock (syncLock)
            {
                if (!_nodes.TryGetValue(nodeId, out WorkflowNode node))
                {
                    return false;
                }
                node.LastHeartbeat = DateTime.UtcNow;
                return true;
            }
        }

        /// <summary>
        /// Updates the heartbeat of a node, returning false if the node no longer exists.
        /// </summary>
        public override async Task<bool> HeartbeatAsync(string nodeId)
        {
            return await Task.FromResult(Heartbeat(nodeId));
        }

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        public override IEnumerable<WorkflowNode> GetNodes()
        {
            lock (syncLock)
            {
                return _nodes.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        public override async Task<IEnumerable<WorkflowNode>> GetNodesAsync()
        {
            return await Task.FromResult(GetNodes());
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override void RemoveNode(string nodeId)
        {
            lock (syncLock)
            {
                _nodes.Remove(nodeId);
            }
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override async Task RemoveNodeAsync(string nodeId)
        {
            await Task.FromResult(0);
            RemoveNode(nodeId);
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout, returning the number removed.
        /// </summary>
        public override long RemoveStaleNodes(TimeSpan timeout)
        {
            lock (syncLock)
            {
                DateTime cutoff = DateTime.UtcNow.Subtract(timeout);
                List<string> staleIds = _nodes.Values.Where(x => x.LastHeartbeat < cutoff).Select(x => x.Id).ToList();
                foreach (string id in staleIds)
                {
                    _nodes.Remove(id);
                }
                return staleIds.Count;
            }
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout, returning the number removed.
        /// </summary>
        public override async Task<long> RemoveStaleNodesAsync(TimeSpan timeout)
        {
            return await Task.FromResult(RemoveStaleNodes(timeout));
        }

        private bool IsClaimable(Guid workflowId, string nodeId)
        {
            if (!_activeWorkflows.ContainsKey(workflowId))
            {
                return false;
            }
            return !_locks.TryGetValue(workflowId, out StoreLock storeLock) || storeLock.NodeId == nodeId;
        }

        private void Release(IEnumerable<Guid> workflowIds)
        {
            foreach (Guid id in workflowIds)
            {
                _locks.Remove(id);
                if (_activeWorkflows.TryGetValue(id, out Workflow workflow))
                {
                    workflow.LockedBy = null;
                    workflow.LockedOn = null;
                }
            }
        }

        private void SetLock(Workflow workflow)
        {
            if (workflow.LockedBy == null)
            {
                _locks.Remove(workflow.Id);
            }
            else if (!_locks.TryGetValue(workflow.Id, out StoreLock storeLock) || storeLock.NodeId != workflow.LockedBy)
            {
                // a save by the lock owner keeps the claim time, as the other stores do
                _locks[workflow.Id] = new StoreLock(workflow.LockedBy, workflow.LockedOn ?? DateTime.UtcNow);
            }
        }

        private sealed class StoreLock
        {
            public StoreLock(string nodeId, DateTime lockedOn)
            {
                this.NodeId = nodeId;
                this.LockedOn = lockedOn;
            }

            public string NodeId { get; }

            public DateTime LockedOn { get; }
        }

    }
}
