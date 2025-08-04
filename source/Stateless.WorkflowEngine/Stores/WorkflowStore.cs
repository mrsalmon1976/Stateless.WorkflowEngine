﻿using Newtonsoft.Json;
using Stateless.WorkflowEngine.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id"></param>
        void Delete(Guid id);

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
        /// Gets the count of unsuspended workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        long GetActiveCount();

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName);

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        IEnumerable<WorkflowDefinition> GetDefinitions();

        /// <summary>
        /// Gets the count of workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        long GetIncompleteCount();

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
        /// Gets a completed workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Workflow GetCompleted(Guid id);

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        long GetCompletedCount();

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Workflow GetCompletedOrDefault(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        Workflow GetOrDefault(Guid id);


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
        /// Gets all incomplete workflows as JSON documents.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IEnumerable<string> GetIncompleteWorkflowsAsJson(int count);

        /// <summary>
        /// Gets the json version of a workflow.
        /// </summary>
        /// <param name="connectionModel"></param>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        string GetWorkflowAsJson(Guid id);

        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        long GetSuspendedCount();

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions);


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        void RegisterType(Type t);

        /// <summary>
        /// Saves an existing workflow to the persistence store.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        void Save(Workflow workflowInfo);

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        void Save(IEnumerable<Workflow> workflows);

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        void SaveDefinition(WorkflowDefinition workflowDefinition);

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        void SuspendWorkflow(Guid id);

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        void UnsuspendWorkflow(Guid id);
    }

    public abstract class WorkflowStore : IWorkflowStore
    {
        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public abstract void Archive(Workflow workflow);

        /// <summary>
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public abstract void Delete(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Workflow Get(Guid id)
        {
            Workflow workflow = this.GetOrDefault(id);
            if (workflow == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            return workflow;
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public T Get<T>(Guid id) where T : Workflow
        {
            return (T)this.Get(id);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public abstract long GetActiveCount();

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public abstract WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName);

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<WorkflowDefinition> GetDefinitions();

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public abstract long GetIncompleteCount();

        /// <summary>
        /// Gets all incomplete workflows as JSON documents.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public virtual IEnumerable<string> GetIncompleteWorkflowsAsJson(int count)
        {
            return this.GetIncomplete(count).Select(x => JsonConvert.SerializeObject(x));
        }
        
        /// <summary>
        /// Gets a workflow as a JSON document.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual string GetWorkflowAsJson(Guid id)
        {
            var doc = this.GetOrDefault(id);
            if (doc == null)
            {
                return null;
            }
            return JsonConvert.SerializeObject(doc);
        }


        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<T> GetAllByType<T>() where T : Workflow
        {
            IEnumerable<Workflow> workflows = this.GetAllByType(typeof(T).AssemblyQualifiedName);
            return workflows.Cast<T>();
        }

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<Workflow> GetAllByType(string workflowType);

        /// <summary>
        /// Gets a completed workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Workflow GetCompleted(Guid id)
        {
            Workflow workflow = this.GetCompletedOrDefault(id);
            if (workflow == null)
            {
                throw new WorkflowNotFoundException(String.Format("No completed workflow found matching id {0}", id));
            }
            return workflow;
        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public abstract long GetCompletedCount();


        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract Workflow GetCompletedOrDefault(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public abstract Workflow GetOrDefault(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetOrDefault<T>(Guid id) where T : Workflow
        {
            Workflow workflow = this.GetOrDefault(id);
            if (workflow == null)
            {
                return default(T);
            }
            return (T)workflow;
        }

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract IEnumerable<Workflow> GetActive(int count);


        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract Task<IEnumerable<Workflow>> GetActiveAsync(int count);


        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract IEnumerable<Workflow> GetIncomplete(int count);


        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public abstract long GetSuspendedCount();

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public abstract void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions);


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        public abstract void RegisterType(Type t);

        /// <summary>
        /// Saves an existing workflow to the persistence store.
        /// </summary>
        /// <param name="workflowInfo"></param>
        public abstract void Save(Workflow workflowInfo);

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public abstract void Save(IEnumerable<Workflow> workflows);

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public abstract void SaveDefinition(WorkflowDefinition workflowDefinition);

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public abstract void SuspendWorkflow(Guid id);

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public abstract void UnsuspendWorkflow(Guid id);


    }
    
}
