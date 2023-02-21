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
                AddScript(Scripts.VizJs.Default);
                AddScript(Scripts.VizJs.FullRender);
                return Default();
            };
            // gets a workflow definition from a specific workflow store
            Get[Actions.Store.Definition] = (x) =>
            {
                return Definition();
            };
            // displays a list of workflows for a specified store
            Post[Actions.Store.List] = (x) =>
            {
                return List();
            };
            // deletes a collection of workflows in a single database
            Post[Actions.Store.Remove] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.RemoveWorkflow });
                return Remove();
            };
            // suspends a collection of workflows in a single database
            Post[Actions.Store.Suspend] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.SuspendWorkflow });
                return Suspend();
            };
            // unsuspends a collection of workflows in a single database
            Post[Actions.Store.Unsuspend] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.UnsuspendWorkflow });
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
            var connection = _userStore.GetConnection(id);

            if (connection == null)
            {
                throw new ArgumentException("No connection found matching the supplied id");
            }

            var currentUser = this.Context.CurrentUser;
            StoreViewModel viewModel = new StoreViewModel();
            viewModel.Connection = connection;
            viewModel.IsSuspendButtonVisible = currentUser.HasClaim(Claims.SuspendWorkflow);
            viewModel.IsUnsuspendButtonVisible = currentUser.HasClaim(Claims.UnsuspendWorkflow);
            viewModel.IsDeleteWorkflowButtonVisible = currentUser.HasClaim(Claims.RemoveWorkflow);
            return this.View[Views.Store.Default, viewModel];
        }

        public dynamic Definition()
        {
            var connectionId = Request.Query["id"];
            var qualifiedName = Request.Query["qname"];

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetConnection(connectionId);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.BadRequest);
            }

            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connection);
            WorkflowDefinition workflowDefinition = workflowStore.GetDefinitionByQualifiedName(qualifiedName);

            if (workflowDefinition == null)
            {
                return this.Response.AsJson(new { Message = "No workflow definition found matching the qualified name" }, HttpStatusCode.NotFound);
            }

            return this.Response.AsJson(workflowDefinition);
        }

        public dynamic List()
        {
            var model = this.Bind<WorkflowListModel>();

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetConnection(model.ConnectionId);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
            }
            
            IEnumerable<UIWorkflow> workflows = _workflowInfoService.GetIncompleteWorkflows(connection, model.WorkflowCount);
            var currentUser = this.Context.CurrentUser;

            WorkflowListViewModel viewModel = new WorkflowListViewModel();
            viewModel.ConnectionId = connection.Id;
            viewModel.Workflows.AddRange(workflows);
            viewModel.IsSuspendButtonVisible = currentUser.HasClaim(Claims.SuspendWorkflow);
            viewModel.IsUnsuspendButtonVisible = currentUser.HasClaim(Claims.UnsuspendWorkflow);
            viewModel.IsDeleteWorkflowButtonVisible = currentUser.HasClaim(Claims.RemoveWorkflow);


            return this.View[Views.Store.ListPartial, viewModel];

        }

        public dynamic Remove()
        {
            var model = this.Bind<WorkflowDeleteModel>();

            // get the connection and load the workflows
            ConnectionModel connection = _userStore.GetConnection(model.ConnectionId);
            if (connection == null)
            {
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
            }

            IWorkflowStore workflowStore = _workflowStoreFactory.GetWorkflowStore(connection);
            foreach (var workflowId in model.WorkflowIds)
            {
                workflowStore.Delete(workflowId);
            }
            return Response.AsJson("{ }");
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
