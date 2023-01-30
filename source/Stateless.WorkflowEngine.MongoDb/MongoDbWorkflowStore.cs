using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Models;
using MongoDB.Driver;
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

        public const string DefaultCollectionActive = "Workflows";
        public const string DefaultCollectionCompleted = "CompletedWorkflows";

        public MongoDbWorkflowStore(IMongoDatabase mongoDatabase) : this(mongoDatabase, DefaultCollectionActive, DefaultCollectionCompleted)
        {
        }

        public MongoDbWorkflowStore(IMongoDatabase mongoDatabase, string activeCollectionName, string completedCollectionName)
        {
            this.MongoDatabase = mongoDatabase;
            this.CollectionActive = activeCollectionName;
            this.CollectionCompleted = completedCollectionName;

            this.SchemaService = new MongoDbSchemaService();
        }

        internal IMongoDbSchemaService SchemaService { get; set; }

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
        public IMongoDatabase MongoDatabase { get; set; }

        public IMongoCollection<MongoWorkflow> GetCollection()
        {
            return this.MongoDatabase.GetCollection<MongoWorkflow>(this.CollectionActive);
        }

        public IMongoCollection<MongoWorkflow> GetCompletedCollection()
        {
            return this.MongoDatabase.GetCollection<MongoWorkflow>(this.CollectionCompleted);
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            var coll = GetCollection();
            coll.DeleteOne(x => x.Id == workflow.Id);

            var collCompleted = GetCompletedCollection();
            collCompleted.InsertOne(new MongoWorkflow(workflow));
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override void Delete(Guid id)
        {
            var coll = GetCollection();
            coll.DeleteOne(x => x.Id == id);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            var collection = GetCollection();
            return collection
                .Find(x => x.Workflow.IsSuspended == false)
                .CountDocuments();
        }

        /// <summary>
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            var collection = GetCollection();
            return collection.Find(x => x.WorkflowType == workflowType)
                .SortBy(x => x.Workflow.CreatedOn)
                .Project(y => y.Workflow)
                .ToEnumerable();

        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override long GetCompletedCount()
        {
            var collection = GetCompletedCollection();
            // use EstimatedDocumentCount for this, as CountDocuments does an actual scan of the underlying documents and can lead to 
            // performance issues on very large collections
            return collection.EstimatedDocumentCount();
        }


        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetCompletedOrDefault(Guid id)
        {
            var collection = GetCompletedCollection();
            Workflow workflow = null;
            var mongoWorkflow = collection.Find(x => x.Id == id).SingleOrDefault();
            if (mongoWorkflow != null)
            {
                workflow = mongoWorkflow.Workflow;
            }
            return workflow;
        }

        public override IEnumerable<string> GetIncompleteWorkflowsAsJson(int count)
        {
            var docs = this.MongoDatabase
                .GetCollection<BsonDocument>(this.CollectionActive)
                .Find(new BsonDocument())
                .Limit(count)
                .ToEnumerable();
            List<string> workflows = new List<string>();
            foreach (BsonDocument document in docs)
            {
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
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
            MongoWorkflow wc = collection.Find(x => x.Id == id).SingleOrDefault();
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
            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(id));
            var doc = this.MongoDatabase
                .GetCollection<BsonDocument>(this.CollectionActive)
                .Find(filter)
                .SingleOrDefault();
            if (doc != null)
            {
                var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.Shell, GuidRepresentation = GuidRepresentation.CSharpLegacy, Indent = true };
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(doc, settings);
                return json;
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
            var collection = GetCollection();
            return collection
                   .Find(x => x.Workflow.IsSuspended == false && (x.Workflow.ResumeOn <= DateTime.UtcNow))
                   .Limit(count)
                   .SortByDescending(x => x.Workflow.Priority)
                   .ThenByDescending(x => x.Workflow.RetryCount)
                   .ThenBy(x => x.Workflow.CreatedOn)
                   .Project(y => y.Workflow)
                   .ToEnumerable();
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            var collection = GetCollection();
            return
                collection
                .Find(x => (x.Workflow.ResumeOn <= DateTime.UtcNow))
                .Limit(count)
                .SortByDescending(x => x.Workflow.Priority)
                .ThenByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Project(x => x.Workflow)
                .ToEnumerable();
        }


        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override long GetSuspendedCount()
        {
            var collection = GetCollection();
            return collection
                .Find(x => x.Workflow.IsSuspended == true)
                .CountDocuments();
        }

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override void Initialise(bool autoCreateTables, bool autoCreateIndexes)
        {
            if (autoCreateTables || autoCreateIndexes)
            {
                this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionActive);
                this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionCompleted);
            }

            if (autoCreateIndexes)
            {
                this.SchemaService.EnsureActiveIndexExists(this.MongoDatabase, this.CollectionActive);
                this.SchemaService.EnsureCompletedIndexExists(this.MongoDatabase, this.CollectionCompleted);
            }
        }


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        public override void RegisterType(Type t)
        {
            MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(t);
        }


        /// <summary>
        /// Updates the specified workflow.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public override void Save(Workflow workflow)
        {
            var coll = GetCollection();
            MongoWorkflow wc = new MongoWorkflow(workflow);
            coll.ReplaceOne(x => x.Id == wc.Id, wc, new ReplaceOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override void Save(IEnumerable<Workflow> workflows)
        {
            var coll = GetCollection();
            var containers = workflows.Select(x => new MongoWorkflow(x));
            foreach (Workflow wf in workflows)
            {
                MongoWorkflow wc = new MongoWorkflow(wf);
                coll.ReplaceOne(x => x.Id == wc.Id, wc, new ReplaceOptions() { IsUpsert = true });
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            var coll = this.MongoDatabase.GetCollection<BsonDocument>(this.CollectionActive);
            BsonValue val = BsonValue.Create(id);

            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(id));
            var doc = coll
                .Find(filter)
                .SingleOrDefault();

            if (doc != null)
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(true);
                coll.ReplaceOne(filter, doc, new ReplaceOptions() { IsUpsert = false });
            }
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            var coll = this.MongoDatabase.GetCollection<BsonDocument>(this.CollectionActive);
            BsonValue val = BsonValue.Create(id);

            var filter = Builders<BsonDocument>.Filter.Eq("_id", BsonValue.Create(id));
            var doc = coll
                .Find(filter)
                .SingleOrDefault();

            if (doc != null)
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(false);
                workflowElement["RetryCount"] = BsonValue.Create(0);
                workflowElement["ResumeOn"] = BsonValue.Create(DateTime.UtcNow);
                coll.ReplaceOne(filter, doc, new ReplaceOptions() { IsUpsert = false });
            }
        }



    }
}
