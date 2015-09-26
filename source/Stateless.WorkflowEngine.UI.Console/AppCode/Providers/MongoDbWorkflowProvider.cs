using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using Stateless.WorkflowEngine.UI.Console.AppCode.Factories;
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
        private MongoDatabase _db;

        public WorkflowStoreConnection Connection { get; set; }

        public MongoDbWorkflowProvider(WorkflowStoreConnection conn)
        {
            string pwd = conn.DecryptPassword();

            MongoUrlBuilder urlBuilder = new MongoUrlBuilder();
            urlBuilder.Server = new MongoServerAddress(conn.Host, conn.Port);
            urlBuilder.DatabaseName = conn.DatabaseName;
            if (!String.IsNullOrWhiteSpace(conn.UserName)) urlBuilder.Username = conn.UserName;
            if (!String.IsNullOrWhiteSpace(pwd)) urlBuilder.Password = pwd;

            var url = urlBuilder.ToMongoUrl();
            var client = new MongoClient(url);
            var server = client.GetServer();
            _db = server.GetDatabase(conn.DatabaseName);

            this.Connection = conn;
        }

        public IEnumerable<UIWorkflowContainer> GetActive(int count)
        {
            var docs = _db.GetCollection(this.Connection.ActiveCollection).FindAll().Take(count);
            List<UIWorkflowContainer> workflowContainers = new List<UIWorkflowContainer>();
            foreach (BsonDocument document in docs)
            {
                string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(document);
                workflowContainers.Add(wc);
            }
            return workflowContainers;
        }

        public void SuspendWorkflow(Guid id)
        {
            BsonValue val = BsonValue.Create(id);
            var coll = _db.GetCollection(this.Connection.ActiveCollection);
            BsonDocument doc = coll.FindOneById(val);
            if (doc != null)
            {
                BsonValue workflowElement = doc["Workflow"];
                workflowElement["IsSuspended"] = BsonValue.Create(true);
                coll.Save(doc);
            }
        }

        public void UnsuspendWorkflow(Guid id)
        {
            BsonValue val = BsonValue.Create(id);
            var coll = _db.GetCollection(this.Connection.ActiveCollection);
            BsonDocument doc = coll.FindOneById(val);
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
