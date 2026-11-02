using System;

namespace Stateless.WorkflowEngine.Events
{
    /// <summary>
    /// Raised when a workflow server node finds that it has been removed by another node (because its heartbeat was
    /// not updated within the node timeout), and has re-registered with a new node id.
    /// </summary>
    public class NodeEvictedEventArgs : EventArgs
    {
        public NodeEvictedEventArgs(string previousNodeId, string nodeId)
        {
            this.PreviousNodeId = previousNodeId;
            this.NodeId = nodeId;
        }

        /// <summary>
        /// Gets/sets the id of the node that was removed.
        /// </summary>
        public string PreviousNodeId { get; set; }

        /// <summary>
        /// Gets/sets the new id the node registered with.
        /// </summary>
        public string NodeId { get; set; }
    }

    /// <summary>
    /// Raised when the background heartbeat/monitoring of a workflow server node fails.  The node keeps running, but
    /// stops claiming new workflows if it cannot heartbeat for longer than half the node timeout.
    /// </summary>
    public class NodeMonitorErrorEventArgs : EventArgs
    {
        public NodeMonitorErrorEventArgs(string nodeId, Exception exception)
        {
            this.NodeId = nodeId;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets/sets the id of the node.
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// Gets/sets the exception that occurred.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
