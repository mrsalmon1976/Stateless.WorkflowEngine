using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System.Collections.Generic;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard
{
    public class CustomDashboardViewModel : BaseViewModel
    {
        public CustomDashboardViewModel()
        {
            this.Connections = new List<ConnectionViewModel>();
        }

        public List<ConnectionViewModel> Connections { get; set; }

        public bool CurrentUserCanAdd { get; set; }

        public bool CurrentUserCanDelete { get; set; }
    }
}
