using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Nancy.ModelBinding;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Encryption;
using AutoMapper;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class ConnectionModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;
        private IConnectionValidator _connectionValidator;
        private IEncryptionProvider _encryptionProvider;
        private IWorkflowInfoService _workflowInfoService;
        private IWorkflowStoreFactory _workflowStoreFactory;

        public ConnectionModule(IUserStore userStore, IConnectionValidator connectionValidator, IEncryptionProvider encryptionProvider, IWorkflowInfoService workflowStoreService, IWorkflowStoreFactory workflowStoreFactory)
            : base()
        {
            _userStore = userStore;
            _connectionValidator = connectionValidator;
            _encryptionProvider = encryptionProvider;
            _workflowInfoService = workflowStoreService;
            _workflowStoreFactory = workflowStoreFactory;

            // lists all connections for the current user
            Get[Actions.Connection.List] = (x) =>
            {
                //this.RequiresAnyClaim(Claims.AllClaims);
                return this.List();
            };
            // deletes a connection for the current user
            Post[Actions.Connection.Delete] = (x) =>
            {
                this.RequiresClaims(Claims.ConnectionDelete);
                return DeleteConnection();
            };
            Post[Actions.Connection.Info] = (x) =>
            {
                return Info();
            };
            // saves a connection for the current user
            Post[Actions.Connection.Save] = (x) =>
            {
                this.RequiresClaims(Claims.ConnectionAdd);
                return Save();
            };
            // tests a new connection for the current user
            Post[Actions.Connection.Test] = (x) =>
            {
                //this.RequiresAnyClaim(Claims.ConnectionAdd);
                return Test();
            };
        }

        public dynamic DeleteConnection()
        {
            var id = Request.Form["id"];

            // load the connections for the current user
            var conn = _userStore.GetConnection(id);
            if (conn == null)
            {
                var model = new { Error = "Connection not found" };
                return this.Response.AsJson(model, HttpStatusCode.NotFound);
            }

            _userStore.Connections.Remove(conn);
            _userStore.Save();
            return this.Response.AsJson(new { Result = "Ok" }, HttpStatusCode.OK);
        }

        public dynamic Info()
        {
            var id = Request.Form["id"];

            // load the connections for the current user
            var conn = _userStore.GetConnection(id);
            if (conn == null)
            {
                var notFoundResult = new { Error = "Connection not found" };
                return this.Response.AsJson(notFoundResult, HttpStatusCode.NotFound);
            }

            ConnectionInfoViewModel infoModel = _workflowInfoService.GetWorkflowStoreInfo(conn);
            return this.Response.AsJson<ConnectionInfoViewModel>(infoModel);
        }

        public dynamic List()
        {
            // load the connections 
            var connections = _userStore.Connections;
            ConnectionListViewModel model = new ConnectionListViewModel();
            List<ConnectionViewModel> connectionViewModels = Mapper.Map<List<ConnectionModel>, List<ConnectionViewModel>>(connections);
            model.Connections.AddRange(connectionViewModels.OrderBy(x => x.Host.ToUpper()).ThenBy(x => x.Database.ToUpper()));
            
            model.CurrentUserCanDeleteConnection = this.Context.CurrentUser.HasClaim(Claims.ConnectionDelete);
            return this.View[Views.Connection.List, model]; ;

        }

        public dynamic Save()
        {
            var viewModel = this.Bind<ConnectionViewModel>();

            var validationResult = _connectionValidator.Validate(viewModel);

            if (!validationResult.Success)
            {
                return Response.AsJson<ValidationResult>(validationResult);
            }

            ConnectionModel model = Mapper.Map<ConnectionViewModel, ConnectionModel>(viewModel);

            // encrypt the password if it's set
            if (!String.IsNullOrEmpty(model.Password))
            {
                byte[] key = _encryptionProvider.NewKey();
                model.Key = Convert.ToBase64String(key);
                model.Password = _encryptionProvider.SimpleEncrypt(model.Password, key);
            }

            // add the connection and return success
            _userStore.Connections.Add(model);
            _userStore.Save();

            return Response.AsJson<ValidationResult>(new ValidationResult());
        }

        public dynamic Test()
        {
            var viewModel = this.Bind<ConnectionViewModel>();

            var validationResult = _connectionValidator.Validate(viewModel);

            if (!validationResult.Success)
            {
                return Response.AsJson<ValidationResult>(validationResult);
            }

            // try and connect
            try
            {
                ConnectionModel model = Mapper.Map<ConnectionViewModel, ConnectionModel>(viewModel);
                // encrypt the password if it's set
                if (!String.IsNullOrEmpty(model.Password))
                {
                    byte[] key = _encryptionProvider.NewKey();
                    model.Key = Convert.ToBase64String(key);
                    model.Password = _encryptionProvider.SimpleEncrypt(model.Password, key);
                }

                // create a store and try and get the active count - this will bomb out if there is a problem
                IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(model);
                store.GetIncompleteCount();

                // all good, return an empty validation result
                return Response.AsJson<ValidationResult>(new ValidationResult());
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (message.ToLower().Contains("not authorized"))
                {
                    message = "Authentication failure";
                }
                return Response.AsJson<ValidationResult>(new ValidationResult("Connection failed: " + message));
            }
        }


    }
}
