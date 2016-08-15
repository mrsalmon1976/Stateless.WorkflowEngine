using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
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

        //IEnumerable<UIWorkflow> ConvertWorkflowDocuments(IEnumerable<string> documents, WorkflowStoreType workflowStoreType);

        IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count);

    }


    public class WorkflowInfoService : IWorkflowInfoService
    {
        private readonly IWorkflowStoreFactory _workflowStoreFactory;

        public WorkflowInfoService(IWorkflowStoreFactory workflowStoreFactory)
        {
            _workflowStoreFactory = workflowStoreFactory;
        }

        //public IEnumerable<UIWorkflow> ConvertWorkflowDocuments(IEnumerable<string> documents, WorkflowStoreType workflowStoreType)
        //{
        //    List<UIWorkflow> workflows = new List<UIWorkflow>();
        //    foreach (string doc in documents)
        //    {
        //        if (workflowStoreType == WorkflowStoreType.MongoDb)
        //        {
        //            UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(doc);
        //            wc.Workflow.WorkflowType = wc.WorkflowType;
        //            workflows.Add(wc.Workflow);
        //        }
        //        else
        //        {
        //            throw new NotImplementedException();
        //        }
        //    }
        //    return workflows;
        //}

        public IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count)
        {
            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);
            IEnumerable<string> documents = workflowStore.GetIncompleteWorkflowsAsJson(count);
            List<UIWorkflow> workflows = new List<UIWorkflow>();

            // for MongoDb, we can't use the GetIncomplete call because the Bson Deserialization call will fail 
            // with unknown types
            if (connectionModel.WorkflowStoreType == WorkflowStoreType.MongoDb)
            {
                foreach (string doc in documents)
                {
                    //string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                    UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(doc);
                    wc.Workflow.WorkflowType = wc.WorkflowType;
                    workflows.Add(wc.Workflow);
                }
            }
            else
            {
                foreach (string doc in documents)
                {
                    //string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                    UIWorkflow wf = JsonConvert.DeserializeObject<UIWorkflow>(doc);
                    workflows.Add(wf);
                }
            }

            return workflows;
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
