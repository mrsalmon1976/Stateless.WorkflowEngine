using System;

namespace Stateless.WorkflowEngine.Events
{
    /// <summary>
    /// Raised when a workflow server claims a workflow for execution, before it is executed.
    /// </summary>
    public class WorkflowClaimedEventArgs : WorkflowEventArgs
    {
        public WorkflowClaimedEventArgs(Workflow workflow, string nodeId) : base(workflow)
        {
            this.NodeId = nodeId;
        }

        /// <summary>
        /// Gets/sets the id of the node that claimed the workflow.
        /// </summary>
        public string NodeId { get; set; }
    }
}
