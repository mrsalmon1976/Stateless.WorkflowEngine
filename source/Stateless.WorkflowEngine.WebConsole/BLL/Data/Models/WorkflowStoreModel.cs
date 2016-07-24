using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Data.Models
{
    public class WorkflowStoreModel
    {
        public WorkflowStoreModel(ConnectionModel connectionModel)
        {
            this.ConnectionModel = connectionModel;
        }

        public ConnectionModel ConnectionModel { get; set; }

        public string ConnectionError { get; set; }

        public long? ActiveCount { get; set; }

        public long? SuspendedCount { get; set; }

        public long? CompletedCount { get; set; }
    }
}
