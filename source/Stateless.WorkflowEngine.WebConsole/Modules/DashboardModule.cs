using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
