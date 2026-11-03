using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.MongoDb
{
    internal interface IMongoDbSchemaService
    {
        void EnsureCollectionExists(IMongoDatabase database, string collectionName);

        Task EnsureCollectionExistsAsync(IMongoDatabase database, string collectionName);

        void EnsureActiveIndexExists(IMongoDatabase database, string collectionName);

        Task EnsureActiveIndexExistsAsync(IMongoDatabase database, string collectionName);

        void EnsureCompletedIndexExists(IMongoDatabase database, string collectionName);

        Task EnsureCompletedIndexExistsAsync(IMongoDatabase database, string collectionName);

        void EnsureSuspendedIndexExists(IMongoDatabase database, string collectionName);

        Task EnsureSuspendedIndexExistsAsync(IMongoDatabase database, string collectionName);

        void EnsureLockedByIndexExists(IMongoDatabase database, string collectionName);

        Task EnsureLockedByIndexExistsAsync(IMongoDatabase database, string collectionName);
    }

    internal class MongoDbSchemaService : IMongoDbSchemaService
    {
        public void EnsureCollectionExists(IMongoDatabase database, string collectionName)
        {
            var collections = database.ListCollectionNames().ToList();

            if (!collections.Contains(collectionName))
            {
                database.CreateCollection(collectionName);
            }
        }

        public async Task EnsureCollectionExistsAsync(IMongoDatabase database, string collectionName)
        {
            var cursor = await database.ListCollectionNamesAsync();
            var collections = await cursor.ToListAsync();

            if (!collections.Contains(collectionName))
            {
                await database.CreateCollectionAsync(collectionName);
            }
        }

        public void EnsureActiveIndexExists(IMongoDatabase database, string collectionName)
        {
            EnsureIndexExists(database.GetCollection<MongoWorkflow>(collectionName), ActiveIndex());
        }

        public async Task EnsureActiveIndexExistsAsync(IMongoDatabase database, string collectionName)
        {
            await EnsureIndexExistsAsync(database.GetCollection<MongoWorkflow>(collectionName), ActiveIndex());
        }

        public void EnsureCompletedIndexExists(IMongoDatabase database, string collectionName)
        {
            EnsureIndexExists(database.GetCollection<MongoWorkflow>(collectionName), CompletedIndex());
        }

        public async Task EnsureCompletedIndexExistsAsync(IMongoDatabase database, string collectionName)
        {
            await EnsureIndexExistsAsync(database.GetCollection<MongoWorkflow>(collectionName), CompletedIndex());
        }

        public void EnsureSuspendedIndexExists(IMongoDatabase database, string collectionName)
        {
            EnsureIndexExists(database.GetCollection<MongoWorkflow>(collectionName), SuspendedIndex());
        }

        public async Task EnsureSuspendedIndexExistsAsync(IMongoDatabase database, string collectionName)
        {
            await EnsureIndexExistsAsync(database.GetCollection<MongoWorkflow>(collectionName), SuspendedIndex());
        }

        public void EnsureLockedByIndexExists(IMongoDatabase database, string collectionName)
        {
            EnsureIndexExists(database.GetCollection<MongoWorkflow>(collectionName), LockedByIndex());
        }

        public async Task EnsureLockedByIndexExistsAsync(IMongoDatabase database, string collectionName)
        {
            await EnsureIndexExistsAsync(database.GetCollection<MongoWorkflow>(collectionName), LockedByIndex());
        }

        private static CreateIndexModel<MongoWorkflow> ActiveIndex()
        {
            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.Priority)
                .Descending(wf => wf.Workflow.RetryCount)
                .Ascending(wf => wf.Workflow.CreatedOn);

            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.Workflow_Priority_RetryCount_CreatedOn;

            return new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
        }

        private static CreateIndexModel<MongoWorkflow> CompletedIndex()
        {
            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.CreatedOn);

            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.CompletedWorkflow_CreatedOn;

            return new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
        }

        /// <summary>
        /// Creates a partial index containing only suspended workflows.  Suspended workflows should be rare, so the
        /// index stays small and cheap to maintain, while making suspended counts and queries index-only.  Being
        /// partial, the query planner only uses it for queries that filter on IsSuspended == true.
        /// </summary>
        private static CreateIndexModel<MongoWorkflow> SuspendedIndex()
        {
            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Ascending(wf => wf.Workflow.IsSuspended);

            var indexOptions = new CreateIndexOptions<MongoWorkflow>();
            indexOptions.Name = IndexNames.Workflow_IsSuspended;
            indexOptions.PartialFilterExpression = Builders<MongoWorkflow>.Filter.Eq(wf => wf.Workflow.IsSuspended, true);

            return new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
        }

        /// <summary>
        /// Creates a partial index containing only locked workflows.  The lock fields are only stored while a workflow
        /// is locked, so the index only contains the workflows currently being executed, and keeps the lock owner and
        /// lock release queries that the workflow server nodes run on every heartbeat cheap.
        /// </summary>
        private static CreateIndexModel<MongoWorkflow> LockedByIndex()
        {
            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Ascending(wf => wf.Workflow.LockedBy);

            var indexOptions = new CreateIndexOptions<MongoWorkflow>();
            indexOptions.Name = IndexNames.Workflow_LockedBy;
            indexOptions.PartialFilterExpression = Builders<MongoWorkflow>.Filter.Exists(wf => wf.Workflow.LockedBy);

            return new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
        }

        private void EnsureIndexExists(IMongoCollection<MongoWorkflow> collection, CreateIndexModel<MongoWorkflow> indexModel)
        {
            IEnumerable<BsonDocument> indexDocuments = collection.Indexes.List().ToList();
            if (IndexExists(collection, indexDocuments, indexModel))
            {
                return;
            }
            collection.Indexes.CreateOne(indexModel);
        }

        private async Task EnsureIndexExistsAsync(IMongoCollection<MongoWorkflow> collection, CreateIndexModel<MongoWorkflow> indexModel)
        {
            var cursor = await collection.Indexes.ListAsync();
            IEnumerable<BsonDocument> indexDocuments = await cursor.ToListAsync();
            if (IndexExists(collection, indexDocuments, indexModel))
            {
                return;
            }
            await collection.Indexes.CreateOneAsync(indexModel);
        }

        private bool IndexExists(IMongoCollection<MongoWorkflow> collection, IEnumerable<BsonDocument> indexDocuments, CreateIndexModel<MongoWorkflow> indexModel)
        {
            string indexName = indexModel.Options.Name;
            var keysDocument = indexModel.Keys.Render(new RenderArgs<MongoWorkflow>(collection.DocumentSerializer, collection.Settings.SerializerRegistry));

            foreach (BsonDocument index in indexDocuments)
            {
                string currentIndexName = index.GetValue("name").AsString;
                if (indexName == currentIndexName)
                {
                    return true;
                }

                var indexDocument = index["key"].AsBsonDocument;

                if (indexDocument.Equals(keysDocument))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
