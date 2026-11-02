using System;

namespace Stateless.WorkflowEngine
{
    /// <summary>
    /// Represents a running workflow server (node).  Each node registers itself in the workflow store and updates
    /// <c>LastHeartbeat</c> periodically - nodes that stop heartbeating are removed by the other nodes, and the
    /// workflows they had claimed are released.
    /// </summary>
    public class WorkflowNode
    {
        /// <summary>
        /// Gets/sets the unique id of the node.  This is the value stored in <c>Workflow.LockedBy</c>.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets/sets a display name for the node (defaults to the machine name).  This is not used for identity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets/sets the UTC date/time the node was registered.
        /// </summary>
        public DateTime StartedOn { get; set; }

        /// <summary>
        /// Gets/sets the UTC date/time of the last successful heartbeat.
        /// </summary>
        public DateTime LastHeartbeat { get; set; }
    }
}
