using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Store
{
    public class WorkflowListModel
    {
        public Guid ConnectionId { get; set; }

        public int WorkflowCount { get; set; }
    }
}
