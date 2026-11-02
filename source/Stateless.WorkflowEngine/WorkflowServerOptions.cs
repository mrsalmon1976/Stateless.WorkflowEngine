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
            this.NodeName = Environment.MachineName;
            this.HeartbeatInterval = TimeSpan.FromSeconds(10);
            this.NodeTimeout = TimeSpan.FromSeconds(60);
        }

        public bool AutoCreateTables { get; set; }

        public bool AutoCreateIndexes { get; set; }

        public bool PersistWorkflowDefinitions { get; set; }

        /// <summary>
        /// Gets/sets the unique id of the workflow server node.  Defaults to null, in which case a new Guid is generated
        /// for each workflow server instance.  If set, it must be unique across all servers using the same store.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// Gets/sets the display name of the node - defaults to the machine name.  This is a label only, and does not
        /// need to be unique.
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// Gets/sets how often the node updates its heartbeat and checks for nodes that have stopped.  Defaults to
        /// 10 seconds.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; }

        /// <summary>
        /// Gets/sets how long a node can go without a heartbeat before other nodes consider it dead, remove it, and
        /// release the workflows it had claimed.  Must be at least 3 times the <c>HeartbeatInterval</c>.  Defaults
        /// to 60 seconds.
        /// </summary>
        public TimeSpan NodeTimeout { get; set; }

    }
}
