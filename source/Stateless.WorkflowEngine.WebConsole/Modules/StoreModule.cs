using Nancy;
using Nancy.Security;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Store;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class StoreModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;
        private IWorkflowInfoService _workflowInfoService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        public StoreModule(IUserStore userStore, IWorkflowInfoService workflowInfoService, IWorkflowStoreFactory workflowStoreFactory) : base()
        {
            _userStore = userStore;
            _workflowInfoService = workflowInfoService;
            _workflowStoreFactory = workflowStoreFactory;
            this.RequiresAnyClaim(Roles.AllRoles);

            // default action - used to display a single connection
            Get[Actions.Store.Default] = (x) =>
            {
                AddScript(Scripts.StoreView);
                return Default();
            };
            // displays a list of workflows for a specified store
            Post[Actions.Store.List] = (x) =>
            {
                return List();
            };
            // loads a single workflow
            Post[Actions.Store.Workflow] = (x) =>
            {
                return Workflow();
            };
        }

        public dynamic Default()
        {
            var id = Request.Query["id"];
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            StoreViewModel model = new StoreViewModel();
            model.Connection = _userStore.GetUser(currentUser.UserName).Connections.Where(x => x.Id == id).SingleOrDefault();

            if (model.Connection == null)
            {
                throw new Exception("No connection found matching the supplied id");
            }

            return this.View[Views.Store.Default, model];
        }


        public dynamic List()
        {
            var id = Request.Form["id"];
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetUser(currentUser.UserName).Connections.Where(x => x.Id == id).SingleOrDefault();
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
                //throw new Exception("No connection found matching the supplied id.");
            }
            
            //List<string> documents = _workflowStoreFactory.GetWorkflowStore(connection).GetIncompleteWorkflowsAsJson(50).ToList();
            //IEnumerable<UIWorkflow> workflows = _workflowInfoService.ConvertWorkflowDocuments(documents, connection.WorkflowStoreType);
            IEnumerable<UIWorkflow> workflows = _workflowInfoService.GetIncompleteWorkflows(connection, 50);
            
            WorkflowListViewModel model = new WorkflowListViewModel();
            model.ConnectionId = connection.Id;
            model.Workflows.AddRange(workflows);

            return this.View[Views.Store.ListPartial, model];

        }

        public dynamic Workflow()
        {
            var workflowId = Request.Form["WorkflowId"];
            var connId = Request.Form["ConnectionId"];
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetUser(currentUser.UserName).Connections.Where(x => x.Id == connId).SingleOrDefault();

            IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(connection);
            string json = store.GetWorkflowAsJson(workflowId);
            if (json == null)
            {
                return this.Response.AsJson(new { Message = "No workflow found matching the supplied workflow and connection" }, HttpStatusCode.NotFound);
            }
            return Response.AsText(json);

        }

    }
}
