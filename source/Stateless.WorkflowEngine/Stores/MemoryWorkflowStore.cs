using System;
using System.Collections.Generic;
using System.Linq;

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
        private static object syncLock = new object();

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            _activeWorkflows.Remove(workflow.Id);
            _completedWorkflows.Add(workflow.Id, workflow);
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override void Delete(Guid id)
        {
            _activeWorkflows.Remove(id);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetActiveCount()
        {
            return this._activeWorkflows.Where(x => x.Value.IsSuspended == false).Count();
        }

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName)
        {
            return this._workflowDefinitions.Where(x => x.QualifiedName == qualifiedName).SingleOrDefault();
        }

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<WorkflowDefinition> GetDefinitions()
        {
            return _workflowDefinitions;
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            return this._activeWorkflows.Count();
        }

		/// <summary>
		/// Gets all workflows of a specified fully qualified name ordered by create date.
		/// </summary>
		/// <returns></returns>
		public override IEnumerable<Workflow> GetAllByQualifiedName(string qualifiedName)
        {
            return this._activeWorkflows.Values
                       .Where(x => x.GetType().FullName == qualifiedName)
                       .OrderBy(x => x.CreatedOn);
        }

        /// <summary>
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            return this._activeWorkflows.Values
                       .Where(x => x.GetType().AssemblyQualifiedName == workflowType)
                       .OrderBy(x => x.CreatedOn);
        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override long GetCompletedCount()
        {
            return this._completedWorkflows.Count;
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetCompletedOrDefault(Guid id)
        {
            if (_completedWorkflows.ContainsKey(id))
            {
                return _completedWorkflows[id];
            }

            return null;
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetOrDefault(Guid id)
        {
            if (_activeWorkflows.ContainsKey(id))
            {
                return _activeWorkflows[id];
            }

            return null;
        }

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            return _activeWorkflows.Values
                .Where(x => !x.IsSuspended && x.ResumeOn <= DateTime.UtcNow)
                .OrderByDescending(x => x.Priority)
                .ThenByDescending(x => x.RetryCount)
                .ThenBy(x => x.CreatedOn)
                .Take(count);
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            return _activeWorkflows.Values
                .Where(x => x.ResumeOn <= DateTime.UtcNow)
                .OrderByDescending(x => x.Priority)
                .ThenByDescending(x => x.RetryCount)
                .ThenBy(x => x.CreatedOn)
                .Take(count);
        }


        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override long GetSuspendedCount()
        {
            return this._activeWorkflows.Values.Where(x => x.IsSuspended == true).Count();
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
            if (_activeWorkflows.ContainsKey(workflow.Id))
            {
                _activeWorkflows[workflow.Id] = workflow;
            }
            else
            {
                _activeWorkflows.Add(workflow.Id, workflow);
            }
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
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override void SaveDefinition(WorkflowDefinition workflowDefinition)
        {
            lock (syncLock)
            {
                WorkflowDefinition existingDefinition = this.GetDefinitionByQualifiedName(workflowDefinition.QualifiedName);
                if (existingDefinition != null)
                {
                    this._workflowDefinitions.Remove(existingDefinition);
                }
                this._workflowDefinitions.Add(workflowDefinition);
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            Workflow w = this.Get(id);
            w.IsSuspended = true;
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            Workflow w = this.Get(id);
            w.IsSuspended = false;
            w.RetryCount = 0;
            w.ResumeOn = DateTime.UtcNow;
        }

    }
}
