using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Utils;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IWorkflowInfoService
    {
        ConnectionInfoViewModel GetWorkflowStoreInfo(ConnectionModel connectionModel);

        IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count);

        string GetWorkflowDefinition(ConnectionModel connectionModel, string qualifiedWorkflowName);

        UIWorkflow GetWorkflowInfoFromJson(string json, WorkflowStoreType workflowStoreType);

        IEnumerable<string> GetWorkflowTypes(ConnectionModel connectionModel);

        CustomDashboardInfoViewModel GetCustomDashboardInfo(CustomDashboardModel dashboard, ConnectionModel connectionModel);
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
            IEnumerable<string> documents = workflowStore.GetIncompleteWorkflowsAsJson(count);
            List<UIWorkflow> workflows = new List<UIWorkflow>();
            Dictionary<string, string> workflowGraphs = new Dictionary<string, string>();

            foreach (string doc in documents)
            {
                var wf = GetWorkflowInfoFromJson(doc, connectionModel.WorkflowStoreType);

                if (!String.IsNullOrEmpty(wf.QualifiedName))    // for backward compatibility only
                {
                    if (workflowGraphs.ContainsKey(wf.QualifiedName))
                    {
                        wf.WorkflowGraph = workflowGraphs[wf.QualifiedName];
                    }
                    else
                    {
                        WorkflowDefinition workflowDefinition = workflowStore.GetDefinitionByQualifiedName(wf.QualifiedName);
                        wf.WorkflowGraph = workflowDefinition?.Graph;
                        workflowGraphs.Add(wf.QualifiedName, workflowDefinition?.Graph);
                    }
                }
                
                workflows.Add(wf);
            }

            return workflows;
        }

        /// <summary>
        /// Converts a JSON workflow into a UIWorkflow object.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="workflowStoreType"></param>
        /// <returns></returns>
        public UIWorkflow GetWorkflowInfoFromJson(string json, WorkflowStoreType workflowStoreType)
        {
            // for MongoDb, we can't use the GetIncomplete call because the Bson Deserialization call will fail 
            // with unknown types
            if (workflowStoreType == WorkflowStoreType.MongoDb)
            {
                //string json = MongoDB.Bson.BsonExtensionMethods.ToJson<BsonDocument>(document);
                UIWorkflowContainer wc = BsonSerializer.Deserialize<UIWorkflowContainer>(json);
                UIWorkflow workflow = wc.Workflow;

                // this for backward-compatibility only - workflows registed prior to v3.0 did not 
                // have a Name property.  Change made 03/02/2023 - this can be removed in future 
                // versions although it does no harm being here
                if (String.IsNullOrEmpty(workflow.Name))
                {
                    if (!String.IsNullOrEmpty(wc.WorkflowType))
                    {
                        ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName(wc.WorkflowType);
                        string className = p.TypeName;
                        int loc = className.LastIndexOf(".");
                        if (loc > -1)
                        {
                            workflow.Name = className.Substring(loc + 1);
                        }
                    }
                }

                return workflow;
            }
            else
            {
                return JsonConvert.DeserializeObject<UIWorkflow>(json);
            }
        }



        public ConnectionInfoViewModel GetWorkflowStoreInfo(ConnectionModel connectionModel)
        {
            if (connectionModel == null) throw new ArgumentNullException("Null connection model supplied");

            ConnectionInfoViewModel model = new ConnectionInfoViewModel();
            try
            {
                IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);
                model.ActiveCount = workflowStore.GetActiveCount();
                model.SuspendedCount = workflowStore.GetSuspendedCount();
                model.CompleteCount = workflowStore.GetCompletedCount();
            }
            catch (Exception ex)
            {
                model.ConnectionError = ex.Message;
            }

            return model;
        }

        public string GetWorkflowDefinition(ConnectionModel connectionModel, string qualifiedWorkflowName)
        {
            if (connectionModel == null) throw new ArgumentNullException("Null connection model supplied");

            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);
            return workflowStore.GetDefinitionByQualifiedName(qualifiedWorkflowName)?.Graph;
        }

        public IEnumerable<string> GetWorkflowTypes(ConnectionModel connectionModel)
        {
            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);

            var definitions = workflowStore.GetDefinitions().ToList();
            if (definitions.Any())
            {
                return definitions
                    .Select(x => x.QualifiedName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x)
                    .ToList();
            }

            return workflowStore.GetIncomplete(200)
                .Select(x => x.QualifiedName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        public CustomDashboardInfoViewModel GetCustomDashboardInfo(CustomDashboardModel dashboard, ConnectionModel connectionModel)
        {
            var result = new CustomDashboardInfoViewModel();
            try
            {
                IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);
                foreach (string qualifiedName in dashboard.WorkflowQualifiedNames)
                {
                    string shortName = qualifiedName.Contains('.')
                        ? qualifiedName.Substring(qualifiedName.LastIndexOf('.') + 1)
                        : qualifiedName;
                    result.WorkflowTypeCounts.Add(new WorkflowTypeCountViewModel
                    {
                        QualifiedName = qualifiedName,
                        ShortName = shortName,
                        ActiveCount = workflowStore.GetActiveCountByQualifiedName(qualifiedName),
                        SuspendedCount = workflowStore.GetSuspendedCountByQualifiedName(qualifiedName),
                        CompletedCount = workflowStore.GetCompletedCountByQualifiedName(qualifiedName)
                    });
                }
            }
            catch (Exception ex)
            {
                result.ConnectionError = ex.Message;
            }
            return result;
        }
    }
}
