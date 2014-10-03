using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Stateless.WorkflowEngine.Stores;

namespace Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Stores workflows in Raven Db.
    /// </summary>
    public class MongoDbWorkflowStore : WorkflowStore
    {

        private Func<Guid, IMongoQuery> _queryById = delegate(Guid id) { return Query<WorkflowContainer>.EQ(x => x.Id, id); };
        private Func<Guid, IMongoQuery> _queryCompletedById = delegate(Guid id) { return Query<CompletedWorkflow>.EQ(x => x.Id, id); };

        public const string DefaultCollectionActive = "Workflows";
        public const string DefaultCollectionCompleted = "CompletedWorkflows";

        public MongoDbWorkflowStore(MongoDatabase mongoDatabase) : this(mongoDatabase, DefaultCollectionActive, DefaultCollectionCompleted)
        {
        }

        public MongoDbWorkflowStore(MongoDatabase mongoDatabase, string activeCollectionName, string completedCollectionName)
        {
            this.MongoDatabase = mongoDatabase;
            this.CollectionActive = activeCollectionName;
            this.CollectionCompleted = completedCollectionName;
        }

        /// <summary>
        /// Gets/sets the name of the MongoDb collection holding active workflows.  Defaults to "Workflows".
        /// </summary>
        public string CollectionActive { get; set; }

        /// <summary>
        /// Gets/sets the name of the MongoDb collection holding completed workflows.  Defaults to "CompletedWorkflows".
        /// </summary>
        public string CollectionCompleted { get; set; }

        /// <summary>
        /// Gets/sets the mongo database associated with the store.
        /// </summary>
        public MongoDatabase MongoDatabase { get; set; }

        public MongoCollection<WorkflowContainer> GetCollection()
        {
            return this.MongoDatabase.GetCollection<WorkflowContainer>(this.CollectionActive);
        }

        public MongoCollection<CompletedWorkflow> GetCompletedCollection()
        {
            return this.MongoDatabase.GetCollection<CompletedWorkflow>(this.CollectionCompleted);
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            var coll = GetCollection();
            coll.Remove(_queryById(workflow.Id));

            var collCompleted = GetCompletedCollection();
            collCompleted.Insert(new CompletedWorkflow(workflow));

        }

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            var collection = GetCollection();
            var query = Query<WorkflowContainer>.Where(x => x.WorkflowType == workflowType);
            return from s in collection.Find(query)
                .OrderByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                   select s.Workflow;
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override CompletedWorkflow GetCompletedOrDefault(Guid id)
        {
            var collection = GetCompletedCollection();
            return collection.FindOne(_queryCompletedById(id));
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetOrDefault(Guid id)
        {
            Workflow workflow = null;
            var collection = GetCollection();
            WorkflowContainer wc = collection.FindOne(_queryById(id));
            if (wc != null)
            {
                workflow = wc.Workflow;
            }
            return workflow;
        }

        /// <summary>
        /// Gets the first <c>count</c> unsuspended active workflows, ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            var collection = GetCollection();
            var query = Query<WorkflowContainer>.Where(x => x.Workflow.IsSuspended == false && (x.Workflow.ResumeOn <= DateTime.UtcNow));
            return from s in collection.Find(query)
                .OrderByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Take(count)
                   select s.Workflow;
        }

        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        public override void RegisterType(Type t)
        {
                //logger.Info("Registering workflow type {0}", t.FullName);
                MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(t);
        }


        /// <summary>
        /// Updates the specified workflow.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public override void Save(Workflow workflow)
        {
            var coll = GetCollection();
            coll.Save(new WorkflowContainer(workflow));
        }

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override void Save(IEnumerable<Workflow> workflows)
        {
            var coll = GetCollection();
            foreach (Workflow wf in workflows) {
                coll.Save(new WorkflowContainer(wf));
            }
        }


    }
}
