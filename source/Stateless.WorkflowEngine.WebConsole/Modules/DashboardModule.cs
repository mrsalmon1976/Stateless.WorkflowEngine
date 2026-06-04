using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System.Linq;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class DashboardModule : WebConsoleSecureModule
    {
        private readonly IUserStore _userStore;

        public DashboardModule(IUserStore userStore) : base()
        {
            _userStore = userStore;

            Get[Actions.Dashboard.Default] = (x) =>
            {
                AddScript(Scripts.DashboardView);
                return this.View[Views.Dashboard.Default, this.Default()];
            };
        }

        public DashboardViewModel Default()
        {
            DashboardViewModel model = new DashboardViewModel();
            model.CustomDashboards.AddRange(_userStore.CustomDashboards.OrderBy(x => x.Name));
            model.CurrentUserCanManageDashboards =
                this.Context.CurrentUser.HasClaim(Claims.CustomDashboardAdd) ||
                this.Context.CurrentUser.HasClaim(Claims.CustomDashboardDelete);
            return model;
        }
    }
}
