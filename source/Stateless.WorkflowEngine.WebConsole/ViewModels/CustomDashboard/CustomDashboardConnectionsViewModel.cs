using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System.Collections.Generic;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard
{
    public class CustomDashboardConnectionsViewModel
    {
        public CustomDashboardConnectionsViewModel()
        {
            this.Connections = new List<ConnectionViewModel>();
        }

        public CustomDashboardModel Dashboard { get; set; }

        public List<ConnectionViewModel> Connections { get; set; }

        public bool CurrentUserCanDeleteConnection { get; set; }
    }
}
