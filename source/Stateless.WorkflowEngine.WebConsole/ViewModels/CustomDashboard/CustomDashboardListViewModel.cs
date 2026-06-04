using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System.Collections.Generic;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard
{
    public class CustomDashboardListViewModel
    {
        public CustomDashboardListViewModel()
        {
            this.Dashboards = new List<CustomDashboardModel>();
        }

        public List<CustomDashboardModel> Dashboards { get; set; }

        public bool CurrentUserCanDelete { get; set; }

        public bool CurrentUserCanAdd { get; set; }
    }
}
