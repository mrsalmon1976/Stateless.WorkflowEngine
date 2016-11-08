using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Models;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Stateless.WorkflowEngine.Stores;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Stores workflows in Raven Db.
    /// </summary>
    public class MongoDbWorkflowStore : WorkflowStore
    {

        private Func<Guid, IMongoQuery> _queryById = delegate(Guid id) { return Query<WorkflowContainer>.EQ(x => x.Id, id); };
        private Func<Guid, IMongoQuery> _queryCompletedById = delegate(Guid id) { return Query<CompletedWorkflow>.EQ(x => x.Id, id); };
        private Func<Guid, IMongoQuery> _queryByIdJson = delegate(Guid id) { return Query.EQ("_id", BsonValue.Create(id)); };

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
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override void Delete(Guid id)
        {
            var coll = GetCollection();
            coll.Remove(_queryById(id));
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            var collection = GetCollection();
            var query = Query<WorkflowContainer>.Where(x => x.Workflow.IsSuspended == false);
            return collection.Find(query).Count();
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
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override long GetCompletedCount()
        {
            var collection = GetCompletedCollection();
            return collection.Count();
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

        public override IEnumerable<string> GetIncompleteWorkflowsAsJson(int count)
        {
            var docs = this.MongoDatabase.GetCollection(this.CollectionActive).FindAll().Take(count);
            List<string> workflows = new List<string>();
            foreach (BsonDocument document in docs)
            {
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                //UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(document);
                //wc.Workflow.WorkflowType = wc.WorkflowType;
                //workflows.Add(wc.Workflow);
                workflows.Add(json);

            }
            return workflows;
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
        /// Gets the json version of a workflow.
        /// </summary>
        /// <param name="connectionModel"></param>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public override string GetWorkflowAsJson(Guid id)
        {
            var doc = this.MongoDatabase.GetCollection(this.CollectionActive).FindOne(_queryByIdJson(id));
            if (doc != null)
            {
                //doc.Add("Id", doc.GetValue("_id"));
                //doc.Set("CreatedOn", BsonValue.Create(doc.GetValue("CreatedOn").ToString()));
                var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.Shell, GuidRepresentation = GuidRepresentation.CSharpLegacy, CloseOutput = true, Indent = true };
                return doc.ToJson(settings);
            }
            return null;
        }


        /// <summary>
        /// Gets the first <c>count</c> unsuspended active workflows, ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            var collection = GetCollection();
            var query = Query<MongoDbWorkflowContainer>.Where(x => x.Workflow.IsSuspended == false && (x.Workflow.ResumeOn <= DateTime.UtcNow));
            return from s in collection.Find(query)
                .OrderByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Take(count)
                   select s.Workflow;
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            var collection = GetCollection();
            var query = Query<MongoDbWorkflowContainer>.Where(x => (x.Workflow.ResumeOn <= DateTime.UtcNow));
            return from s in collection.Find(query)
                .OrderByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Take(count)
                   select s.Workflow;
        }


        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override long GetSuspendedCount()
        {
            var collection = GetCollection();
            var query = Query<WorkflowContainer>.Where(x => x.Workflow.IsSuspended == true);
            return collection.Find(query).Count();
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

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            BsonValue val = BsonValue.Create(id);
            var coll = this.MongoDatabase.GetCollection(this.CollectionActive);
            BsonDocument doc = coll.FindOneById(val);
            if (doc != null)
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(true);
                coll.Save(doc);
            }
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            BsonValue val = BsonValue.Create(id);
            var coll = this.MongoDatabase.GetCollection(this.CollectionActive);
            BsonDocument doc = coll.FindOneById(id);
            if (doc != null)
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(false);
                workflowElement["RetryCount"] = BsonValue.Create(0);
                workflowElement["ResumeOn"] = BsonValue.Create(DateTime.UtcNow);
                coll.Save(doc);
            }

        }



    }
}
