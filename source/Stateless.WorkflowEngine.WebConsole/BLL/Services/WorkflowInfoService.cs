using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IWorkflowInfoService
    {
        void PopulateWorkflowStoreInfo(WorkflowStoreModel workflowStoreModel);

        IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count);
    }


    public class WorkflowInfoService : IWorkflowInfoService
    {
        private readonly IWorkflowStoreFactory _workflowStoreFactory;

        public WorkflowInfoService(IWorkflowStoreFactory workflowStoreFactory)
        {
            _workflowStoreFactory = workflowStoreFactory;
        }

        public IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count)
        {
            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);

            // for MongoDb, we can't use the GetIncomplete call because the Bson Deserialization call will fail 
            // with unknown types
            if (connectionModel.WorkflowStoreType == WorkflowStoreType.MongoDb)
            {
                MongoDbWorkflowStore mongoStore = (MongoDbWorkflowStore)workflowStore;
                var docs = mongoStore.MongoDatabase.GetCollection(connectionModel.ActiveCollection).FindAll().Take(count);
                List<UIWorkflow> workflows = new List<UIWorkflow>();
                foreach (BsonDocument document in docs)
                {
                    string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                    UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(document);
                    wc.Workflow.WorkflowType = wc.WorkflowType;
                    workflows.Add(wc.Workflow);
                }
                return workflows;
            }

            throw new NotImplementedException();
        }


        public void PopulateWorkflowStoreInfo(WorkflowStoreModel workflowStoreModel)
        {
            if (workflowStoreModel == null) throw new ArgumentNullException("workflowStoreModel");

            try
            {
                IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(workflowStoreModel.ConnectionModel);
                workflowStoreModel.ActiveCount = workflowStore.GetIncompleteCount();
                workflowStoreModel.SuspendedCount = workflowStore.GetSuspendedCount();
                workflowStoreModel.CompletedCount = workflowStore.GetCompletedCount();
            }
            catch (Exception ex)
            {
                workflowStoreModel.ConnectionError = ex.Message;
            }
        }
    }
}
