using System;
using System.Collections.Generic;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public class WorkflowServerOptions
    {
        public WorkflowServerOptions()
        {
            this.AutoCreateIndexes = true;
            this.AutoCreateTables = true;
            this.PersistWorkflowDefinitions = true;
        }

        public bool AutoCreateTables { get; set; }

        public bool AutoCreateIndexes { get; set; }

        public bool PersistWorkflowDefinitions { get; set; }

    }
}
