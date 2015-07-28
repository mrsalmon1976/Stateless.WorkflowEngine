using MongoDB.Driver;
using Stateless.WorkflowEngine.UI.Console.AppCode;
using Stateless.WorkflowEngine.UI.Console.Models.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Services.Workflow
{
    public class WorkflowProviderFactory
    {
        public static IWorkflowProvider GetWorkflowService(WorkflowStoreConnection conn)
        {
            switch (conn.WorkflowStoreType)
            {
                case WorkflowStoreType.MongoDb:
                    return GetMongoDbWorkflowService(conn);
                default:
                    throw new NotSupportedException();
            }
        }

        private static IWorkflowProvider GetMongoDbWorkflowService(WorkflowStoreConnection conn)
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
            MongoDatabase db = server.GetDatabase(conn.DatabaseName);
            return new MongoDbWorkflowProvider(db, conn.ActiveCollection, conn.CompleteCollection);
        }
    }
}
