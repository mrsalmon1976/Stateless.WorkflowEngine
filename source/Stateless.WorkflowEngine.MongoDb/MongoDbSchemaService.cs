using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    internal interface IMongoDbSchemaService
    {
        void EnsureCollectionExists(IMongoDatabase database, string collectionName);

        void EnsureActiveIndexExists(IMongoDatabase database, string collectionName);

        void EnsureCompletedIndexExists(IMongoDatabase database, string collectionName);
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

        public void EnsureActiveIndexExists(IMongoDatabase database, string collectionName)
        {
            var collection = database.GetCollection<MongoWorkflow>(collectionName);

            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.Priority)
                .Descending(wf => wf.Workflow.RetryCount)
                .Ascending(wf => wf.Workflow.CreatedOn);

            bool indexExists = IndexExists(collection, IndexNames.Workflow_Priority_RetryCount_CreatedOn, indexKeys);
            if (indexExists)
            {
                return;
            }


            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.Workflow_Priority_RetryCount_CreatedOn;

            var indexModel = new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
            collection.Indexes.CreateOne(indexModel);
        }

        public void EnsureCompletedIndexExists(IMongoDatabase database, string collectionName)
        {
            var collection = database.GetCollection<MongoWorkflow>(collectionName);

            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.CreatedOn);

            bool indexExists = IndexExists(collection, IndexNames.CompletedWorkflow_CreatedOn, indexKeys);
            if (indexExists)
            {
                return;
            }

            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.CompletedWorkflow_CreatedOn;

            var indexModel = new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
            collection.Indexes.CreateOne(indexModel);
        }

        private bool IndexExists(IMongoCollection<MongoWorkflow> collection, string indexName, IndexKeysDefinition<MongoWorkflow> indexKeys)
        {
            IEnumerable<BsonDocument> indexDocuments = collection.Indexes.List().ToList();
            var keysDocument = indexKeys.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry);

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
