using MongoDB.Bson;
using MongoDB.Driver;
using Stateless.WorkflowEngine.Models;
using System;
using System.Collections.Generic;
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

            bool indexExists = IndexExists(collection.Indexes.List().ToList(), IndexNames.Workflow_Priority_RetryCount_CreatedOn);
            if (indexExists)
            {
                return;
            }


            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.Workflow_Priority_RetryCount_CreatedOn;

            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.Priority)
                .Descending(wf => wf.Workflow.RetryCount)
                .Ascending(wf => wf.Workflow.CreatedOn);

            var indexModel = new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
            collection.Indexes.CreateOne(indexModel);
        }

        public void EnsureCompletedIndexExists(IMongoDatabase database, string collectionName)
        {
            var collection = database.GetCollection<MongoWorkflow>(collectionName);

            bool indexExists = IndexExists(collection.Indexes.List().ToList(), IndexNames.CompletedWorkflow_CreatedOn);
            if (indexExists)
            {
                return;
            }

            var indexOptions = new CreateIndexOptions();
            indexOptions.Name = IndexNames.CompletedWorkflow_CreatedOn;

            var indexKeys = Builders<MongoWorkflow>
                .IndexKeys
                .Descending(wf => wf.Workflow.CreatedOn);

            var indexModel = new CreateIndexModel<MongoWorkflow>(indexKeys, indexOptions);
            collection.Indexes.CreateOne(indexModel);
        }

        private bool IndexExists(IEnumerable<BsonDocument> indexDocuments, string indexName)
        {
            foreach (BsonDocument index in indexDocuments)
            {
                string currentIndexName = index.GetValue("name").AsString;
                if (indexName == currentIndexName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
