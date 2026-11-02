using System;
using System.Collections.Generic;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    public static class IndexNames
    {
        public const string Workflow_Priority_RetryCount_CreatedOn = "Workflow_Priority_RetryCount_CreatedOn";

        public const string CompletedWorkflow_CreatedOn = "CompletedWorkflow_CreatedOn";

        /// <summary>
        /// Partial index containing only suspended workflows.
        /// </summary>
        public const string Workflow_IsSuspended = "Workflow_IsSuspended";

        /// <summary>
        /// Partial index containing only workflows locked by a workflow server node.
        /// </summary>
        public const string Workflow_LockedBy = "Workflow_LockedBy";
    }
}
