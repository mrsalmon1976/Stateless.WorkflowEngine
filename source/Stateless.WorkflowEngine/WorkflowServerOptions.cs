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
            this.WorkflowExecutionTaskCount = 50;
        }

        public bool AutoCreateTables { get; set; }

        public bool AutoCreateIndexes { get; set; }

        public bool PersistWorkflowDefinitions { get; set; }

        /// <summary>
        /// The number of workflows that will be loaded for execution per iteration.  This is the number of tasks 
        /// that will be created for execution before the workflow server execution ends (not the number of 
        /// workflows executed in parallel).
        /// </summary>
        public int WorkflowExecutionTaskCount { get; set; }
    }
}
