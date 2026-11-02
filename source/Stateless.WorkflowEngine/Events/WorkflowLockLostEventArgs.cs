using System;

namespace Stateless.WorkflowEngine.Events
{
    /// <summary>
    /// Raised when a workflow server could not save a workflow it was executing, because the workflow has since been
    /// claimed by another node (or no longer exists).
    /// </summary>
    public class WorkflowLockLostEventArgs : WorkflowEventArgs
    {
        public WorkflowLockLostEventArgs(Workflow workflow, string nodeId) : base(workflow)
        {
            this.NodeId = nodeId;
        }

        /// <summary>
        /// Gets/sets the id of the node that had claimed the workflow.
        /// </summary>
        public string NodeId { get; set; }
    }
}
