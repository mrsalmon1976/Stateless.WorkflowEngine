using Encryption;
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
        private IEncryptionProvider _encryptionProvider;

        public WorkflowStoreFactory(IEncryptionProvider encryptionProvider)
        {
            _encryptionProvider = encryptionProvider;
        }

        public IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel)
        {
            IWorkflowStore workflowStore;
            if (connectionModel.WorkflowStoreType == WorkflowStoreType.MongoDb)
            {
                MongoUrlBuilder urlBuilder = new MongoUrlBuilder();
                urlBuilder.Server = new MongoServerAddress(connectionModel.Host, connectionModel.Port.Value);
                if (!String.IsNullOrWhiteSpace(connectionModel.User))
                {
                    urlBuilder.Username = connectionModel.User;
                }
                if (!String.IsNullOrWhiteSpace(connectionModel.Password) && !String.IsNullOrEmpty(connectionModel.Key))
                {
                    byte[] key = Convert.FromBase64String(connectionModel.Key);
                    string pwd = _encryptionProvider.SimpleDecrypt(connectionModel.Password, key);
                    urlBuilder.Password = pwd;
                }

                if (!String.IsNullOrWhiteSpace(connectionModel.ReplicaSet))
                {
                    urlBuilder.ReplicaSetName = connectionModel.ReplicaSet;
                }

                var url = urlBuilder.ToMongoUrl();
                var client = new MongoClient(url);
                var db = client.GetDatabase(connectionModel.Database);

                workflowStore = new MongoDbWorkflowStore(db, connectionModel.ActiveCollection, connectionModel.CompletedCollection, connectionModel.WorkflowDefinitionCollection);
            }
            else
            {
                throw new NotImplementedException();
            }

            return workflowStore;
        }
    }
}
