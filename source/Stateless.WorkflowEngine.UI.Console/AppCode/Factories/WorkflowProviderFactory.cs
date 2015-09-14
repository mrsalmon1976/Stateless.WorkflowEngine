using MongoDB.Driver;
using Stateless.WorkflowEngine.UI.Console.AppCode;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using Stateless.WorkflowEngine.UI.Console.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.AppCode.Factories
{
    public interface IWorkflowProviderFactory
    {
        IWorkflowProvider GetWorkflowService(WorkflowStoreConnection conn);
    }

    public class WorkflowProviderFactory : IWorkflowProviderFactory
    {
        public IWorkflowProvider GetWorkflowService(WorkflowStoreConnection conn)
        {
            switch (conn.WorkflowStoreType)
            {
                case WorkflowStoreType.MongoDb:
                    return new MongoDbWorkflowProvider(conn);
                default:
                    throw new NotSupportedException();
            }
        }

    }
}
