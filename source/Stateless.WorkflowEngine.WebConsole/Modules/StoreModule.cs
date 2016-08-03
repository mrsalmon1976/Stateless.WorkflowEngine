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

        public StoreModule(IUserStore userStore, IWorkflowInfoService workflowInfoService) : base()
        {
            _userStore = userStore;
            _workflowInfoService = workflowInfoService;
            this.RequiresAnyClaim(Roles.AllRoles);

            Get[Actions.Store.Default] = (x) =>
            {
                AddScript(Scripts.StoreView);
                return Default();
            };
            Post[Actions.Store.List] = (x) =>
            {
                return List();
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
                return this.Response.AsJson(new { Message = "No connection found matching the supplied id" }, HttpStatusCode.NotFound);
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
            }
            
            IEnumerable<UIWorkflow> workflows = _workflowInfoService.GetIncompleteWorkflows(connection, 50);

            WorkflowListViewModel model = new WorkflowListViewModel();
            model.Workflows.AddRange(workflows);

            return this.View[Views.Store.ListPartial, model];

        }

    }
}
