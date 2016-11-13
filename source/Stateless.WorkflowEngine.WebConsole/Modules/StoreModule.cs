using Nancy;
using Nancy.ModelBinding;
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
            // suspends a collection of workflows in a single database
            Post[Actions.Store.Suspend] = (x) =>
            {
                return Suspend();
            };
            // unsuspends a collection of workflows in a single database
            Post[Actions.Store.Unsuspend] = (x) =>
            {
                return Unsuspend();
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

            StoreViewModel model = new StoreViewModel();
            model.Connection = _userStore.GetConnection(id);

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
            ConnectionModel connection = _userStore.GetConnection(id);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
                //throw new Exception("No connection found matching the supplied id.");
            }
            
            IEnumerable<UIWorkflow> workflows = _workflowInfoService.GetIncompleteWorkflows(connection, 50);
            
            WorkflowListViewModel model = new WorkflowListViewModel();
            model.ConnectionId = connection.Id;
            model.Workflows.AddRange(workflows);

            return this.View[Views.Store.ListPartial, model];

        }

        public dynamic Suspend()
        {
            var model = this.Bind<WorkflowSuspensionModel>();
            
            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetConnection(model.ConnectionId);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
            }

            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connection);
            foreach (var workflowId in model.WorkflowIds)
            {
                workflowStore.SuspendWorkflow(workflowId);
            }
            return Response.AsJson("{ }");
        }

        public dynamic Unsuspend()
        {
            var model = this.Bind<WorkflowSuspensionModel>();

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetConnection(model.ConnectionId);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
            }

            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connection);
            foreach (var workflowId in model.WorkflowIds)
            {
                workflowStore.UnsuspendWorkflow(workflowId);
            }
            return Response.AsJson("{ }");
        }

        public dynamic Workflow()
        {
            var workflowId = Request.Form["WorkflowId"];
            var connId = Request.Form["ConnectionId"];
            var currentUser = _userStore.GetUser(this.Context.CurrentUser.UserName);

            // get the connection and load the workflow store
            ConnectionModel connection = _userStore.GetConnection(connId);
            IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(connection);

            // extract the data we need to display the workflow
            string json = store.GetWorkflowAsJson(workflowId);
            if (json == null)
            {
                return this.Response.AsJson(new { Message = "No workflow found matching the supplied workflow and connection (the workflow may have completed)." }, HttpStatusCode.NotFound);
            }
            UIWorkflow wf = _workflowInfoService.GetWorkflowInfoFromJson(json, connection.WorkflowStoreType);

            WorkflowViewModel viewModel = new WorkflowViewModel();
            viewModel.WorkflowJson = json;
            viewModel.IsSuspended = wf.IsSuspended;
            return this.Response.AsJson(viewModel);

        }

    }
}
