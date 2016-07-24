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
            this.WorkflowStores = new List<WorkflowStoreModel>();
        }

        public List<WorkflowStoreModel> WorkflowStores { get; private set; }

    }
}
