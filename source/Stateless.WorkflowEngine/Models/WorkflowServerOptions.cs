using System;
using System.Collections.Generic;
using System.Text;

namespace Stateless.WorkflowEngine.Models
{
    public class WorkflowServerOptions
    {
        public WorkflowServerOptions()
        {
            this.AutoCreateIndexes = true;
            this.AutoCreateTables = true;
        }

        public bool AutoCreateTables { get; set; }

        public bool AutoCreateIndexes { get; set; }
    }
}
