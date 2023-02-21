using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Store
{
    public class StoreViewModel
    {
        public ConnectionModel Connection { get; set; }

        public bool IsSuspendButtonVisible { get; set; }

        public bool IsUnsuspendButtonVisible { get; set; }

        public bool IsDeleteWorkflowButtonVisible { get; set; }

    }
}
