using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Authentication.Forms;
using Nancy.ModelBinding;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using Nancy.Responses.Negotiation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class LoginModule : WebConsoleModule
    {
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;

        public LoginModule(IUserStore userStore, IPasswordProvider passwordProvider)
        {
            _userStore = userStore;
            _passwordProvider = passwordProvider;

            Get["/"] = x =>
            {
                return this.Response.AsRedirect(Actions.Login.Default);
            };

            Get[Actions.Login.Default] = x =>
            {
                AddScript(Scripts.LoginView);
                return this.LoginGet();
            };

            Post[Actions.Login.Default] = x =>
            {
                return LoginPost();
            };

            Get[Actions.Login.Logout] = x =>
            {
                return this.Logout(Actions.Login.Default);
            };

        }

        public dynamic LoginGet()
        {
            var model = this.Bind<LoginViewModel>();
            if (this.Context.CurrentUser != null)
            {
                return this.Response.AsRedirect(Actions.Dashboard.Default);
            }
            if (String.IsNullOrEmpty(model.ReturnUrl))
            {
                model.ReturnUrl = Actions.Dashboard.Default;
            }
            return this.View[Views.Login, model];
        }

        public dynamic LoginPost()
        {
            LoginViewModel model = this.Bind<LoginViewModel>();
            BasicResult result = new BasicResult(false);

            // if the email or password hasn't been supplied, exit
            if ((!String.IsNullOrWhiteSpace(model.UserName)) && (!String.IsNullOrWhiteSpace(model.Password)))
            {
                // get the user
                UserModel user = _userStore.Users.SingleOrDefault(x => x.UserName == model.UserName);
                if (user != null && _passwordProvider.CheckPassword(model.Password, user.Password))
                {
                    result.Success = true;
                    return this.Login(user.Id, DateTime.Now.AddDays(1));
                }
            }

            return this.Response.AsJson(result);
        }
    }
}
