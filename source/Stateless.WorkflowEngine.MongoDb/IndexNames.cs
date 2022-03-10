using System;
using System.Collections.Generic;
using System.Text;

namespace Stateless.WorkflowEngine.MongoDb
{
    public static class IndexNames
    {
        public const string Workflow_Priority_RetryCount_CreatedOn = "Workflow_Priority_RetryCount_CreatedOn";

        public const string CompletedWorkflow_CreatedOn = "CompletedWorkflow_CreatedOn";
    }
}
