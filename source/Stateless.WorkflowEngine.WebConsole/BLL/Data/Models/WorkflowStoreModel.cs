using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Models
{
    public class WorkflowStoreModel
    {
        public string Server { get; set; }

        public int Port { get; set; }

        public string Database { get; set; }

        public string ConnectionError { get; set; }

        public int ActiveCount { get; set; }

        public int SuspendedCount { get; set; }

        public int CompletedCount { get; set; }
    }
}
