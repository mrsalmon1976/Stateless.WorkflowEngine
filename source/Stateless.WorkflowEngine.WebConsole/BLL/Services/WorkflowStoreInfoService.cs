using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IWorkflowStoreInfoService
    {
        void PopulateWorkflowStoreInfo(WorkflowStoreModel workflowStoreModel);
    }


    public class WorkflowStoreInfoService : IWorkflowStoreInfoService
    {
        private readonly IWorkflowClientFactory _workflowClientFactory;

        public WorkflowStoreInfoService(IWorkflowClientFactory workflowClientFactory)
        {
            _workflowClientFactory = workflowClientFactory;
        }

        public void PopulateWorkflowStoreInfo(WorkflowStoreModel workflowStoreModel)
        {
            if (workflowStoreModel == null) throw new ArgumentNullException("workflowStoreModel");

            try
            {
                IWorkflowClient workflowClient = _workflowClientFactory.GetWorkflowClient(workflowStoreModel.ConnectionModel);
                workflowStoreModel.ActiveCount = workflowClient.GetActiveCount();
                workflowStoreModel.SuspendedCount = workflowClient.GetSuspendedCount();
                workflowStoreModel.CompletedCount = workflowClient.GetCompletedCount();
            }
            catch (Exception ex)
            {
                workflowStoreModel.ConnectionError = ex.Message;
            }
        }
    }
}
