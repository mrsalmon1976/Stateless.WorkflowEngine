using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using System;
using System.Linq;
using System.Collections.Generic;

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
        CompletedWorkflow GetCompleted(Guid id);

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        CompletedWorkflow GetCompletedOrDefault(Guid id);

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        Workflow GetOrDefault(Guid id);


        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        IEnumerable<Workflow> GetActive(int count);

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
    }

    public abstract class WorkflowStore : IWorkflowStore
    {
        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public abstract void Archive(Workflow workflow);

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
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAllByType<T>() where T : Workflow
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
        public CompletedWorkflow GetCompleted(Guid id)
        {
            CompletedWorkflow workflow = this.GetCompletedOrDefault(id);
            if (workflow == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            return workflow;
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or returns null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract CompletedWorkflow GetCompletedOrDefault(Guid id);

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
        /// Gets the first <c>count</c> active workflows, ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract IEnumerable<Workflow> GetActive(int count);

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

    }
    
}
