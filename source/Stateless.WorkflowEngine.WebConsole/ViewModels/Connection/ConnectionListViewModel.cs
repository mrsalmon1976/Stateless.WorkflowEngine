using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Connection
{
    public class ConnectionListViewModel : BaseViewModel
    {
        public ConnectionListViewModel()
        {
            this.Connections = new List<ConnectionViewModel>();
        }

        public List<ConnectionViewModel> Connections { get; set; }

        public bool CurrentUserCanDeleteConnection { get; set; }

    }
}
