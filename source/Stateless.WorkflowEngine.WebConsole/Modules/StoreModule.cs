using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using System;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class StoreModule : WebConsoleSecureModule
    {
        private IUserStore _userStore;

        public StoreModule(IUserStore userStore) : base()
        {
            _userStore = userStore;
            this.RequiresAnyClaim();

            Get[Actions.Store.Default] = (x) =>
            {
                AddScript(Scripts.StoreView);
                return Default();
            };
            Get[Actions.Store.Workflows] = (x) =>
            {
                return Workflows();
            };
        }

        public dynamic Default()
        {
            //UserViewModel model = new UserViewModel();
            //model.Roles.AddRange(Roles.AllRoles);
            //model.SelectedRole = Roles.User;
            //return this.View[Views.User.Default, model];
            throw new NotImplementedException();
        }


        public dynamic Workflows()
        {
            //UserListViewModel model = new UserListViewModel();
            //model.Users.AddRange(_userStore.Users);
            //return this.View[Views.User.ListPartial, model];
            throw new NotImplementedException();
        }

    }
}
