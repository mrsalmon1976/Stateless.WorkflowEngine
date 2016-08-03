using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Factories
{
    public interface IWorkflowStoreFactory
    {
        IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel);
    }

    public class WorkflowStoreFactory : IWorkflowStoreFactory
    {
        public IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel)
        {
            IWorkflowStore workflowStore;
            if (connectionModel.WorkflowStoreType == WorkflowStoreType.MongoDb)
            {
                MongoUrlBuilder urlBuilder = new MongoUrlBuilder();
                urlBuilder.Server = new MongoServerAddress(connectionModel.Host, connectionModel.Port.Value);
                if (!String.IsNullOrWhiteSpace(connectionModel.User)) urlBuilder.Username = connectionModel.User;
                //if (!String.IsNullOrWhiteSpace(pwd)) urlBuilder.Password = pwd;

                var url = urlBuilder.ToMongoUrl();
                var client = new MongoClient(url);
                var server = client.GetServer();
                var db = server.GetDatabase(connectionModel.Database);

                workflowStore = new MongoDbWorkflowStore(db, connectionModel.ActiveCollection, connectionModel.CompletedCollection);
            }
            else
            {
                throw new NotImplementedException();
            }

            return workflowStore;
        }
    }
}
