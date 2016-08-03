using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Store
{
    public class WorkflowListViewModel : BaseViewModel
    {
        public WorkflowListViewModel()
            : base()
        {
            this.Workflows = new List<UIWorkflow>();
        }

        public List<UIWorkflow> Workflows { get; private set; }
        

    }
}
