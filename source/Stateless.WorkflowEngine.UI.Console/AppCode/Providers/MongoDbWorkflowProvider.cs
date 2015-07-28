using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Services.Workflow
{
    public class MongoDbWorkflowProvider : IWorkflowProvider
    {
        public MongoDbWorkflowProvider(MongoDatabase db, string activeCollectionName, string completedCollectionName)
        {
            this.MongoDatabase = db;
            this.ActiveCollectionName = activeCollectionName;
            this.CompletedCollectionName = completedCollectionName;
        }

        public string ActiveCollectionName { get; set; }
        public string CompletedCollectionName { get; set; }
        public MongoDatabase MongoDatabase { get; set; }

        public IEnumerable<WorkflowContainer> GetActive(int count)
        {
            var docs = this.MongoDatabase.GetCollection(this.ActiveCollectionName).FindAll().Take(count);
            List<WorkflowContainer> workflowContainers = new List<WorkflowContainer>();
            foreach (BsonDocument document in docs)
            {
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                WorkflowContainer wc = BsonSerializer.Deserialize<WorkflowContainer>(document);
                workflowContainers.Add(wc);
            }
            return workflowContainers;
        }


    }
}
