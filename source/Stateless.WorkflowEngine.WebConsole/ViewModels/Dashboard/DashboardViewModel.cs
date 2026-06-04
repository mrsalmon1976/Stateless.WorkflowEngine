using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard
{
    public class DashboardViewModel : BaseViewModel
    {
        public DashboardViewModel()
        {
            this.CustomDashboards = new List<CustomDashboardModel>();
        }

        public List<CustomDashboardModel> CustomDashboards { get; set; }

        public bool CurrentUserCanManageDashboards { get; set; }
    }
}
