using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.MongoDb
{
    /// <summary>
    /// Stores workflows in MongoDb.
    /// </summary>
    public class MongoDbWorkflowStore : WorkflowStore
    {

        public const string DefaultCollectionActive = "Workflows";
        public const string DefaultCollectionCompleted = "CompletedWorkflows";
        public const string DefaultCollectionDefinitions = "WorkflowDefinitions";
        public const string DefaultCollectionNodes = "WorkflowNodes";

        /// <summary>
        /// The representation used for workflow and definition ids.  Version 3 of the MongoDb driver requires the Guid
        /// representation to be configured, and previous versions of the store (driver 2.x) saved Guids in the C# legacy
        /// representation (binary subtype 3), so the same representation is used to keep existing data readable.
        /// </summary>
        public const GuidRepresentation IdGuidRepresentation = GuidRepresentation.CSharpLegacy;

        static MongoDbWorkflowStore()
        {
            // the id members are configured on the class maps rather than by registering a global Guid serializer, as
            // the driver doesn't allow the global serializer to be changed once it has been used (and the application
            // will usually have created a MongoClient before the store).  Guid properties on custom workflow classes are
            // not covered by this - see the README.
            BsonClassMap.TryRegisterClassMap<Workflow>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(w => w.Id).SetSerializer(new GuidSerializer(IdGuidRepresentation));

                // unlocked workflows don't store the lock fields, so the partial LockedBy index only contains locked workflows
                cm.GetMemberMap(w => w.LockedBy).SetIgnoreIfNull(true);
                cm.GetMemberMap(w => w.LockedOn).SetIgnoreIfNull(true);
            });
            BsonClassMap.TryRegisterClassMap<WorkflowDefinition>(cm =>
            {
                cm.AutoMap();
                cm.GetMemberMap(d => d.Id).SetSerializer(new GuidSerializer(IdGuidRepresentation));
            });
        }

        /// <summary>
        /// Converts an id into a BsonValue for queries on BsonDocument collections, so the value matches the stored _id.
        /// </summary>
        private static BsonValue ToBsonId(Guid id)
        {
            return new BsonBinaryData(id, IdGuidRepresentation);
        }

        private static readonly Regex LegacyGuidHexDataRegex = new Regex("HexData\\(3, \"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\"\\)", RegexOptions.Compiled);

        /// <summary>
        /// Version 3 of the driver writes C# legacy Guids in shell mode as HexData(3, "...") - the raw bytes formatted like
        /// a Guid, which is misleading and can't be parsed back by the driver - so they are converted to CSUUID("...")
        /// as written by previous versions.
        /// </summary>
        private static string ToReadableLegacyGuids(string json)
        {
            return LegacyGuidHexDataRegex.Replace(json, m =>
            {
                byte[] bytes = Convert.FromHexString(m.Groups[1].Value.Replace("-", ""));
                return $"CSUUID(\"{new Guid(bytes)}\")";
            });
        }

        public MongoDbWorkflowStore(IMongoDatabase mongoDatabase) : this(mongoDatabase, DefaultCollectionActive, DefaultCollectionCompleted, DefaultCollectionDefinitions)
        {
        }

        public MongoDbWorkflowStore(IMongoDatabase mongoDatabase, string activeCollectionName, string completedCollectionName, string workflowDefinitionCollectionName)
        {
            this.MongoDatabase = mongoDatabase;
            this.CollectionActive = activeCollectionName ?? DefaultCollectionActive;
            this.CollectionCompleted = completedCollectionName ?? DefaultCollectionCompleted;
            this.CollectionDefinitions = workflowDefinitionCollectionName ?? DefaultCollectionDefinitions;
            this.CollectionNodes = DefaultCollectionNodes;

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
        /// Gets/sets the name of the MongoDb collection holding workflow definitions.  Defaults to "WorkflowDefinitions".
        /// </summary>
        public string CollectionDefinitions { get; set; }

        /// <summary>
        /// Gets/sets the name of the MongoDb collection holding the registered workflow server nodes.  Defaults to "WorkflowNodes".
        /// </summary>
        public string CollectionNodes { get; set; }

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

        public IMongoCollection<WorkflowDefinition> GetDefinitionCollection()
        {
            return this.MongoDatabase.GetCollection<WorkflowDefinition>(this.CollectionDefinitions);
        }

        public IMongoCollection<WorkflowNode> GetNodeCollection()
        {
            return this.MongoDatabase.GetCollection<WorkflowNode>(this.CollectionNodes);
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
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override async Task ArchiveAsync(Workflow workflow)
        {
            var coll = GetCollection();
            await coll.DeleteOneAsync(x => x.Id == workflow.Id);

            var collCompleted = GetCompletedCollection();
            await collCompleted.InsertOneAsync(new MongoWorkflow(workflow));
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
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override async Task DeleteAsync(Guid id)
        {
            var coll = GetCollection();
            await coll.DeleteOneAsync(x => x.Id == id);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <remarks>
        /// Counting non-suspended workflows directly scans (nearly) the whole collection, so this is calculated as the
        /// estimated collection size (from metadata) less the suspended count (which uses the partial suspended
        /// index).  As with GetIncompleteCount, the result is an estimate - it can be slightly out after an unclean
        /// shutdown or when orphaned documents exist in a sharded cluster.
        /// </remarks>
        /// <returns></returns>
        public override long GetActiveCount()
        {
            long count = GetIncompleteCount() - GetSuspendedCount();
            return Math.Max(0, count);
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <remarks>
        /// See GetActiveCount for how this is calculated.
        /// </remarks>
        /// <returns></returns>
        public override async Task<long> GetActiveCountAsync()
        {
            long count = await GetIncompleteCountAsync() - await GetSuspendedCountAsync();
            return Math.Max(0, count);
        }

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName)
        {
            var collection = GetDefinitionCollection();
            return collection
                   .Find(x => x.QualifiedName == qualifiedName)
                   .SingleOrDefault();
        }

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<WorkflowDefinition> GetDefinitions()
        {
            var collection = GetDefinitionCollection();
            return collection.Find(_ => true).ToList();
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            var collection = GetCollection();
            return collection.EstimatedDocumentCount();
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetIncompleteCountAsync()
        {
            var collection = GetCollection();
            return await collection.EstimatedDocumentCountAsync();
        }

		/// <summary>
		/// Gets all workflows of a specified fully qualified name ordered by create date.
		/// </summary>
		/// <returns></returns>
		public override IEnumerable<Workflow> GetAllByQualifiedName(string qualifiedName)
        {
            var collection = GetCollection();
            return collection.Find(x => x.Workflow.QualifiedName == qualifiedName)
                .SortBy(x => x.Workflow.CreatedOn)
                .Project(y => y.Workflow)
                .ToEnumerable();
        }

        /// <summary>
        /// Gets all workflows of a specified fully qualified name ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByQualifiedNameAsync(string qualifiedName)
        {
            var collection = GetCollection();
            var cursor = await collection.Find(x => x.Workflow.QualifiedName == qualifiedName)
                .SortBy(x => x.Workflow.CreatedOn)
                .Project(y => y.Workflow)
                .ToCursorAsync();
            return await cursor.ToListAsync();
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
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByTypeAsync(string workflowType)
        {
            var collection = GetCollection();
            var cursor = await collection.Find(x => x.WorkflowType == workflowType)
                .SortBy(x => x.Workflow.CreatedOn)
                .Project(y => y.Workflow)
                .ToCursorAsync();
            return await cursor.ToListAsync();
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
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetCompletedCountAsync()
        {
            var collection = GetCompletedCollection();
            return await collection.EstimatedDocumentCountAsync();
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

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetCompletedOrDefaultAsync(Guid id)
        {
            var collection = GetCompletedCollection();
            var mongoWorkflow = await collection.Find(x => x.Id == id).SingleOrDefaultAsync();
            return mongoWorkflow?.Workflow;
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
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetOrDefaultAsync(Guid id)
        {
            var collection = GetCollection();
            MongoWorkflow wc = await collection.Find(x => x.Id == id).SingleOrDefaultAsync();
            return wc?.Workflow;
        }

        /// <summary>
        /// Gets the json version of a workflow.
        /// </summary>
        /// <param name="connectionModel"></param>
        /// <param name="workflowId"></param>
        /// <returns></returns>
        public override string GetWorkflowAsJson(Guid id)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ToBsonId(id));
            var doc = this.MongoDatabase
                .GetCollection<BsonDocument>(this.CollectionActive)
                .Find(filter)
                .SingleOrDefault();
            if (doc != null)
            {
                
                var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.Shell, Indent = true };
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(doc, settings);
                return ToReadableLegacyGuids(json);
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
                   .ToList();
        }


        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetActiveAsync(int count)
        {
            var collection = GetCollection();
            var cursor = await collection
                .Find(x => x.Workflow.IsSuspended == false && (x.Workflow.ResumeOn <= DateTime.UtcNow))
                .Limit(count)
                .SortByDescending(x => x.Workflow.Priority)
                .ThenByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Project(y => y.Workflow)
                .ToCursorAsync();

            return await cursor.ToListAsync();
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
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetIncompleteAsync(int count)
        {
            var collection = GetCollection();
            var cursor = await collection
                .Find(x => (x.Workflow.ResumeOn <= DateTime.UtcNow))
                .Limit(count)
                .SortByDescending(x => x.Workflow.Priority)
                .ThenByDescending(x => x.Workflow.RetryCount)
                .ThenBy(x => x.Workflow.CreatedOn)
                .Project(x => x.Workflow)
                .ToCursorAsync();
            return await cursor.ToListAsync();
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
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetSuspendedCountAsync()
        {
            var collection = GetCollection();
            return await collection
                .Find(x => x.Workflow.IsSuspended == true)
                .CountDocumentsAsync();
        }

        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            if (autoCreateTables || autoCreateIndexes)
            {
                this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionActive);
                this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionCompleted);

                this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionNodes);

                if (persistWorkflowDefinitions)
                {
                    this.SchemaService.EnsureCollectionExists(this.MongoDatabase, this.CollectionDefinitions);
                }

            }

            if (autoCreateIndexes)
            {
                this.SchemaService.EnsureActiveIndexExists(this.MongoDatabase, this.CollectionActive);
                this.SchemaService.EnsureSuspendedIndexExists(this.MongoDatabase, this.CollectionActive);
                this.SchemaService.EnsureLockedByIndexExists(this.MongoDatabase, this.CollectionActive);
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
        /// Updates the specified workflow.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public override async Task SaveAsync(Workflow workflow)
        {
            var coll = GetCollection();
            MongoWorkflow wc = new MongoWorkflow(workflow);
            await coll.ReplaceOneAsync(x => x.Id == wc.Id, wc, new ReplaceOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override void Save(IEnumerable<Workflow> workflows)
        {
            var coll = GetCollection();
            foreach (Workflow wf in workflows)
            {
                MongoWorkflow wc = new MongoWorkflow(wf);
                coll.ReplaceOne(x => x.Id == wc.Id, wc, new ReplaceOptions() { IsUpsert = true });
            }
        }

        /// <summary>
        /// Saves a collection of existing workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override async Task SaveAsync(IEnumerable<Workflow> workflows)
        {
            var coll = GetCollection();
            foreach (Workflow wf in workflows)
            {
                MongoWorkflow wc = new MongoWorkflow(wf);
                await coll.ReplaceOneAsync(x => x.Id == wc.Id, wc, new ReplaceOptions() { IsUpsert = true });
            }
        }

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override void SaveDefinition(WorkflowDefinition workflowDefinition)
        {
            var coll = GetDefinitionCollection();
            coll.ReplaceOne(x => x.Id == workflowDefinition.Id, workflowDefinition, new ReplaceOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override async Task SaveDefinitionAsync(WorkflowDefinition workflowDefinition)
        {
            var coll = GetDefinitionCollection();
            await coll.ReplaceOneAsync(x => x.Id == workflowDefinition.Id, workflowDefinition, new ReplaceOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            var coll = this.MongoDatabase.GetCollection<BsonDocument>(this.CollectionActive);

            var filter = Builders<BsonDocument>.Filter.Eq("_id", ToBsonId(id));
            var doc = coll
                .Find(filter)
                .SingleOrDefault();

            if (doc == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            else 
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(true);
                coll.ReplaceOne(filter, doc, new ReplaceOptions() { IsUpsert = false });
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override async Task SuspendWorkflowAsync(Guid id)
        {
            var coll = this.MongoDatabase.GetCollection<BsonDocument>(this.CollectionActive);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ToBsonId(id));
            var doc = await coll.Find(filter).SingleOrDefaultAsync();

            if (doc == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            else 
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(true);
                await coll.ReplaceOneAsync(filter, doc, new ReplaceOptions() { IsUpsert = false });
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

            var filter = Builders<BsonDocument>.Filter.Eq("_id", ToBsonId(id));
            var doc = coll
                .Find(filter)
                .SingleOrDefault();

            if (doc == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            else
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(false);
                workflowElement["RetryCount"] = BsonValue.Create(0);
                workflowElement["ResumeOn"] = BsonValue.Create(DateTime.UtcNow);
                coll.ReplaceOne(filter, doc, new ReplaceOptions() { IsUpsert = false });
            }
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override async Task UnsuspendWorkflowAsync(Guid id)
        {
            var coll = this.MongoDatabase.GetCollection<BsonDocument>(this.CollectionActive);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ToBsonId(id));
            var doc = await coll.Find(filter).SingleOrDefaultAsync();

            if (doc == null)
            {
                throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
            }
            else
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(false);
                workflowElement["RetryCount"] = BsonValue.Create(0);
                workflowElement["ResumeOn"] = BsonValue.Create(DateTime.UtcNow);
                await coll.ReplaceOneAsync(filter, doc, new ReplaceOptions() { IsUpsert = false });
            }
        }

        /// <summary>
        /// Atomically claims the next available workflow for a node, using a single findAndModify so that no two nodes
        /// can claim the same workflow.
        /// </summary>
        public override Workflow ClaimNext(string nodeId)
        {
            var collection = GetCollection();
            DateTime now = DateTime.UtcNow;
            MongoWorkflow mongoWorkflow = collection.FindOneAndUpdate(ClaimFilter(now), ClaimUpdate(nodeId, now), ClaimOptions());
            return mongoWorkflow?.Workflow;
        }

        /// <summary>
        /// Atomically claims the next available workflow for a node, using a single findAndModify so that no two nodes
        /// can claim the same workflow.
        /// </summary>
        public override async Task<Workflow> ClaimNextAsync(string nodeId)
        {
            var collection = GetCollection();
            DateTime now = DateTime.UtcNow;
            MongoWorkflow mongoWorkflow = await collection.FindOneAndUpdateAsync(ClaimFilter(now), ClaimUpdate(nodeId, now), ClaimOptions());
            return mongoWorkflow?.Workflow;
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> workflows for a node in three round trips: the candidates are read in one
        /// query, claimed with a single updateMany that only matches the ones that are still available (each document is
        /// updated atomically, so no two nodes can claim the same workflow), and the ones won by this claim are read back.
        /// </summary>
        public override IEnumerable<Workflow> ClaimNext(string nodeId, int count)
        {
            if (count == 1)
            {
                Workflow workflow = ClaimNext(nodeId);
                return workflow == null ? new List<Workflow>() : new List<Workflow> { workflow };
            }

            var collection = GetCollection();
            while (true)
            {
                DateTime now = NextClaimTime();
                List<Guid> ids = collection.Find(ClaimFilter(now)).Sort(ClaimSort()).Limit(count).Project(x => x.Id).ToList();
                if (ids.Count == 0)
                {
                    return new List<Workflow>();
                }

                collection.UpdateMany(ClaimFilter(now) & Builders<MongoWorkflow>.Filter.In(x => x.Id, ids), ClaimUpdate(nodeId, now));
                List<MongoWorkflow> claimed = collection.Find(ClaimWonFilter(ids, nodeId, now)).Sort(ClaimSort()).ToList();
                if (claimed.Count > 0)
                {
                    return claimed.Select(x => x.Workflow).ToList();
                }
                // other nodes claimed all of the candidates first - try the next ones
            }
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> workflows for a node in three round trips: the candidates are read in one
        /// query, claimed with a single updateMany that only matches the ones that are still available (each document is
        /// updated atomically, so no two nodes can claim the same workflow), and the ones won by this claim are read back.
        /// </summary>
        public override async Task<IEnumerable<Workflow>> ClaimNextAsync(string nodeId, int count)
        {
            if (count == 1)
            {
                Workflow workflow = await ClaimNextAsync(nodeId);
                return workflow == null ? new List<Workflow>() : new List<Workflow> { workflow };
            }

            var collection = GetCollection();
            while (true)
            {
                DateTime now = NextClaimTime();
                List<Guid> ids = await collection.Find(ClaimFilter(now)).Sort(ClaimSort()).Limit(count).Project(x => x.Id).ToListAsync();
                if (ids.Count == 0)
                {
                    return new List<Workflow>();
                }

                await collection.UpdateManyAsync(ClaimFilter(now) & Builders<MongoWorkflow>.Filter.In(x => x.Id, ids), ClaimUpdate(nodeId, now));
                List<MongoWorkflow> claimed = await collection.Find(ClaimWonFilter(ids, nodeId, now)).Sort(ClaimSort()).ToListAsync();
                if (claimed.Count > 0)
                {
                    return claimed.Select(x => x.Workflow).ToList();
                }
                // other nodes claimed all of the candidates first - try the next ones
            }
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override bool SaveClaimed(Workflow workflow, string nodeId)
        {
            var coll = GetCollection();
            var result = coll.ReplaceOne(ClaimedFilter(workflow.Id, nodeId), new MongoWorkflow(workflow), new ReplaceOptions() { IsUpsert = false });
            return result.MatchedCount == 1;
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override async Task<bool> SaveClaimedAsync(Workflow workflow, string nodeId)
        {
            var coll = GetCollection();
            var result = await coll.ReplaceOneAsync(ClaimedFilter(workflow.Id, nodeId), new MongoWorkflow(workflow), new ReplaceOptions() { IsUpsert = false });
            return result.MatchedCount == 1;
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override bool ArchiveClaimed(Workflow workflow, string nodeId)
        {
            var coll = GetCollection();
            var result = coll.DeleteOne(ClaimedFilter(workflow.Id, nodeId));
            if (result.DeletedCount == 0)
            {
                return false;
            }

            var collCompleted = GetCompletedCollection();
            collCompleted.InsertOne(new MongoWorkflow(workflow));
            return true;
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.
        /// </summary>
        public override async Task<bool> ArchiveClaimedAsync(Workflow workflow, string nodeId)
        {
            var coll = GetCollection();
            var result = await coll.DeleteOneAsync(ClaimedFilter(workflow.Id, nodeId));
            if (result.DeletedCount == 0)
            {
                return false;
            }

            var collCompleted = GetCompletedCollection();
            await collCompleted.InsertOneAsync(new MongoWorkflow(workflow));
            return true;
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override void ReleaseLocks(string nodeId)
        {
            GetCollection().UpdateMany(x => x.Workflow.LockedBy == nodeId, ReleaseUpdate());
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override async Task ReleaseLocksAsync(string nodeId)
        {
            await GetCollection().UpdateManyAsync(x => x.Workflow.LockedBy == nodeId, ReleaseUpdate());
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override void ReleaseStaleLocks(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            GetCollection().UpdateMany(StaleLocksFilter(nodeId, lockedBefore, excludeIds), ReleaseUpdate());
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override async Task ReleaseStaleLocksAsync(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            await GetCollection().UpdateManyAsync(StaleLocksFilter(nodeId, lockedBefore, excludeIds), ReleaseUpdate());
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        public override IEnumerable<string> GetLockOwners()
        {
            return GetCollection().Distinct<string>(LockedByField, LockedFilter()).ToList().Where(x => x != null).ToList();
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.
        /// </summary>
        public override async Task<IEnumerable<string>> GetLockOwnersAsync()
        {
            var cursor = await GetCollection().DistinctAsync<string>(LockedByField, LockedFilter());
            return (await cursor.ToListAsync()).Where(x => x != null).ToList();
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current (server) time.
        /// </summary>
        public override void RegisterNode(WorkflowNode node)
        {
            GetNodeCollection().UpdateOne(x => x.Id == node.Id, RegisterNodeUpdate(node), new UpdateOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current (server) time.
        /// </summary>
        public override async Task RegisterNodeAsync(WorkflowNode node)
        {
            await GetNodeCollection().UpdateOneAsync(x => x.Id == node.Id, RegisterNodeUpdate(node), new UpdateOptions() { IsUpsert = true });
        }

        /// <summary>
        /// Updates the heartbeat of a node to the current (server) time, returning false if the node no longer exists.
        /// </summary>
        public override bool Heartbeat(string nodeId)
        {
            var result = GetNodeCollection().UpdateOne(x => x.Id == nodeId, Builders<WorkflowNode>.Update.CurrentDate(x => x.LastHeartbeat));
            return result.MatchedCount == 1;
        }

        /// <summary>
        /// Updates the heartbeat of a node to the current (server) time, returning false if the node no longer exists.
        /// </summary>
        public override async Task<bool> HeartbeatAsync(string nodeId)
        {
            var result = await GetNodeCollection().UpdateOneAsync(x => x.Id == nodeId, Builders<WorkflowNode>.Update.CurrentDate(x => x.LastHeartbeat));
            return result.MatchedCount == 1;
        }

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        public override IEnumerable<WorkflowNode> GetNodes()
        {
            return GetNodeCollection().Find(_ => true).ToList();
        }

        /// <summary>
        /// Gets all registered nodes.
        /// </summary>
        public override async Task<IEnumerable<WorkflowNode>> GetNodesAsync()
        {
            return await GetNodeCollection().Find(_ => true).ToListAsync();
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override void RemoveNode(string nodeId)
        {
            GetNodeCollection().DeleteOne(x => x.Id == nodeId);
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override async Task RemoveNodeAsync(string nodeId)
        {
            await GetNodeCollection().DeleteOneAsync(x => x.Id == nodeId);
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.  The heartbeats are compared to the
        /// server's current time, so this is not affected by clock differences between the workflow servers.
        /// </summary>
        public override long RemoveStaleNodes(TimeSpan timeout)
        {
            return GetNodeCollection().DeleteMany(StaleNodesFilter(timeout)).DeletedCount;
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.  The heartbeats are compared to the
        /// server's current time, so this is not affected by clock differences between the workflow servers.
        /// </summary>
        public override async Task<long> RemoveStaleNodesAsync(TimeSpan timeout)
        {
            var result = await GetNodeCollection().DeleteManyAsync(StaleNodesFilter(timeout));
            return result.DeletedCount;
        }

        private const string LockedByField = "Workflow.LockedBy";

        private static FilterDefinition<MongoWorkflow> ClaimFilter(DateTime now)
        {
            return Builders<MongoWorkflow>.Filter.Where(x => x.Workflow.IsSuspended == false && x.Workflow.ResumeOn <= now && x.Workflow.LockedBy == null);
        }

        private static UpdateDefinition<MongoWorkflow> ClaimUpdate(string nodeId, DateTime now)
        {
            return Builders<MongoWorkflow>.Update
                .Set(x => x.Workflow.LockedBy, nodeId)
                .Set(x => x.Workflow.LockedOn, now);
        }

        private static FindOneAndUpdateOptions<MongoWorkflow> ClaimOptions()
        {
            return new FindOneAndUpdateOptions<MongoWorkflow>()
            {
                Sort = ClaimSort(),
                ReturnDocument = ReturnDocument.After
            };
        }

        private static SortDefinition<MongoWorkflow> ClaimSort()
        {
            return Builders<MongoWorkflow>.Sort
                .Descending(x => x.Workflow.Priority)
                .Descending(x => x.Workflow.RetryCount)
                .Ascending(x => x.Workflow.CreatedOn);
        }

        /// <summary>
        /// Matches the candidates won by a batch claim.  The claim time is unique within the process, so workflows claimed
        /// at the same time by another claim for the same node (e.g. overlapping ExecuteWorkflows calls) are not included.
        /// </summary>
        private static FilterDefinition<MongoWorkflow> ClaimWonFilter(List<Guid> ids, string nodeId, DateTime claimTime)
        {
            var builder = Builders<MongoWorkflow>.Filter;
            return builder.In(x => x.Id, ids)
                & builder.Eq(x => x.Workflow.LockedBy, nodeId)
                & builder.Eq(x => x.Workflow.LockedOn, (DateTime?)claimTime);
        }

        private static long _lastClaimTicks;

        /// <summary>
        /// Gets the time to stamp a batch claim with: now, truncated to the millisecond precision MongoDB stores, and
        /// later than any previous claim time in this process.
        /// </summary>
        private static DateTime NextClaimTime()
        {
            long nowTicks = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond;
            long last, next;
            do
            {
                last = Interlocked.Read(ref _lastClaimTicks);
                next = Math.Max(nowTicks, last + TimeSpan.TicksPerMillisecond);
            }
            while (Interlocked.CompareExchange(ref _lastClaimTicks, next, last) != last);
            return new DateTime(next, DateTimeKind.Utc);
        }

        private static FilterDefinition<MongoWorkflow> ClaimedFilter(Guid id, string nodeId)
        {
            return Builders<MongoWorkflow>.Filter.Where(x => x.Id == id && (x.Workflow.LockedBy == nodeId || x.Workflow.LockedBy == null));
        }

        private static FilterDefinition<MongoWorkflow> LockedFilter()
        {
            return Builders<MongoWorkflow>.Filter.Exists(LockedByField);
        }

        private static FilterDefinition<MongoWorkflow> StaleLocksFilter(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            var builder = Builders<MongoWorkflow>.Filter;
            return builder.Eq(x => x.Workflow.LockedBy, nodeId)
                & builder.Lt(x => x.Workflow.LockedOn, lockedBefore)
                & builder.Nin(x => x.Id, excludeIds);
        }

        private static UpdateDefinition<MongoWorkflow> ReleaseUpdate()
        {
            return Builders<MongoWorkflow>.Update
                .Unset(x => x.Workflow.LockedBy)
                .Unset(x => x.Workflow.LockedOn);
        }

        private static UpdateDefinition<WorkflowNode> RegisterNodeUpdate(WorkflowNode node)
        {
            return Builders<WorkflowNode>.Update
                .Set(x => x.Name, node.Name)
                .Set(x => x.StartedOn, node.StartedOn)
                .CurrentDate(x => x.LastHeartbeat);
        }

        private static FilterDefinition<WorkflowNode> StaleNodesFilter(TimeSpan timeout)
        {
            // { $expr: { $lt: [ "$LastHeartbeat", { $subtract: [ "$$NOW", timeoutMs ] } ] } }
            BsonDocument cutoff = new BsonDocument("$subtract", new BsonArray { "$$NOW", (long)timeout.TotalMilliseconds });
            return new BsonDocument("$expr", new BsonDocument("$lt", new BsonArray { "$LastHeartbeat", cutoff }));
        }

    }
}
