using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Utils;

namespace Stateless.WorkflowEngine.WebConsole.Services
{
    public class WorkflowStoreInfo
    {
        public long ActiveCount { get; set; }

        public long SuspendedCount { get; set; }

        public long CompleteCount { get; set; }

        public string? ConnectionError { get; set; }
    }

    public interface IWorkflowInfoService
    {
        WorkflowStoreInfo GetWorkflowStoreInfo(ConnectionModel connectionModel);

        IEnumerable<UIWorkflow> GetIncompleteWorkflows(ConnectionModel connectionModel, int count);

        UIWorkflow GetWorkflowInfoFromJson(WorkflowStoreType workflowStoreType, string json);
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
            Dictionary<string, string?> workflowGraphs = new Dictionary<string, string?>();

            foreach (string doc in documents)
            {
                var wf = GetWorkflowInfoFromJson(connectionModel.WorkflowStoreType, doc);

                if (!String.IsNullOrEmpty(wf.QualifiedName))    // for backward compatibility only
                {
                    if (!workflowGraphs.TryGetValue(wf.QualifiedName, out string? graph))
                    {
                        graph = workflowStore.GetDefinitionByQualifiedName(wf.QualifiedName)?.Graph;
                        workflowGraphs.Add(wf.QualifiedName, graph);
                    }
                    wf.WorkflowGraph = graph;
                }

                workflows.Add(wf);
            }

            return workflows;
        }

        /// <summary>
        /// Converts a JSON workflow into a UIWorkflow object.  The store's typed Get calls can't be used
        /// because the console does not have the workflow types loaded.
        /// </summary>
        public UIWorkflow GetWorkflowInfoFromJson(WorkflowStoreType workflowStoreType, string json)
        {
            // RavenDb documents are plain JSON - the $type of the workflow is ignored as the type isn't loaded
            UIWorkflowContainer wc = (workflowStoreType == WorkflowStoreType.RavenDb
                ? JsonConvert.DeserializeObject<UIWorkflowContainer>(json)
                : BsonSerializer.Deserialize<UIWorkflowContainer>(json)) ?? new UIWorkflowContainer();
            UIWorkflow workflow = wc.Workflow ?? new UIWorkflow();

            // workflows registered prior to v3.0 did not have a Name property
            if (String.IsNullOrEmpty(workflow.Name) && !String.IsNullOrEmpty(wc.WorkflowType))
            {
                ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName(wc.WorkflowType);
                string className = p.TypeName;
                int loc = className.LastIndexOf('.');
                if (loc > -1)
                {
                    workflow.Name = className.Substring(loc + 1);
                }
            }

            return workflow;
        }

        public WorkflowStoreInfo GetWorkflowStoreInfo(ConnectionModel connectionModel)
        {
            ArgumentNullException.ThrowIfNull(connectionModel);

            WorkflowStoreInfo info = new WorkflowStoreInfo();
            try
            {
                IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connectionModel);
                info.ActiveCount = workflowStore.GetActiveCount();
                info.SuspendedCount = workflowStore.GetSuspendedCount();
                info.CompleteCount = workflowStore.GetCompletedCount();
            }
            catch (Exception ex)
            {
                info.ConnectionError = ex.Message;
            }

            return info;
        }
    }
}
