using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Store
{
    public class MongoDbStoreContainer : IStoreContainer
    {
        public string ActiveCollectionName { get; set; }

        public string CompletedCollectionName { get; set; }

        public string Server { get; set; }

        public string Database { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public int Port { get; set; }

        public string Name
        {
            get
            {
                return String.Format("{0} | {1}", this.Server, this.Database);
            }
        }

        public IWorkflowStore GetStore()
        {
            MongoUrlBuilder urlBuilder = new MongoUrlBuilder();
            urlBuilder.Server = new MongoServerAddress(this.Server, this.Port);
            urlBuilder.DatabaseName = this.Database;
            if (!String.IsNullOrWhiteSpace(this.UserName)) urlBuilder.Username = this.UserName;
            if (!String.IsNullOrWhiteSpace(this.Password)) urlBuilder.Password = this.Password;

            var url = urlBuilder.ToMongoUrl();
            var client = new MongoClient(url);
            var server = client.GetServer();
            MongoDatabase db = server.GetDatabase(this.Database);
            return new MongoDbWorkflowStore(db, this.ActiveCollectionName, this.CompletedCollectionName);
        }
    }
}
