using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Connection
{
    public class ConnectionInfoViewModel
    {
        public long? ActiveCount { get; set; }
        public long? SuspendedCount { get; set; }
        public long? CompleteCount { get; set; }

        public string ConnectionError { get; set; }
    }
}
