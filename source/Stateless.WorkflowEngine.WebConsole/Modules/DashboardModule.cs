using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class DashboardModule : WebConsoleSecureModule
    {
        public DashboardModule() : base()
        {
            Get[Actions.Dashboard.Default] = (x) =>
            {
                AddScript(Scripts.DashboardView);
                return this.View[Views.Dashboard.Default, this.Default()];
            };
        }

        public DashboardViewModel Default()
        {
            DashboardViewModel model = new DashboardViewModel();
            return model;

        }


    }
}
