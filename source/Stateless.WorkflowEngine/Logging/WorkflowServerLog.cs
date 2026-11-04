using Microsoft.Extensions.Logging;
using System;

namespace Stateless.WorkflowEngine.Logging
{
    /// <summary>
    /// Log messages written by the workflow server (event ids 1000-1099).
    /// </summary>
    internal static partial class WorkflowServerLog
    {
        [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Node {NodeId} claimed {Count} workflow(s)")]
        public static partial void BatchClaimed(ILogger logger, string nodeId, int count);

        [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Executing workflow {WorkflowId} ({WorkflowType}): state {State}, trigger {Trigger}")]
        public static partial void ExecutingStep(ILogger logger, Guid workflowId, string workflowType, string state, string trigger);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Workflow {WorkflowId} ({WorkflowType}) completed")]
        public static partial void WorkflowCompleted(ILogger logger, Guid workflowId, string workflowType);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Workflow {WorkflowId} ({WorkflowType}) failed in state {State} - retry {RetryCount} at {ResumeOn:u}")]
        public static partial void WorkflowRetrying(ILogger logger, Exception exception, Guid workflowId, string workflowType, string state, int retryCount, DateTime resumeOn);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Workflow {WorkflowId} ({WorkflowType}) failed in state {State} and has been suspended")]
        public static partial void WorkflowSuspended(ILogger logger, Exception exception, Guid workflowId, string workflowType, string state);

        [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Workflow {WorkflowId} ({WorkflowType}) was not saved: it was claimed by another node while node {NodeId} was executing it")]
        public static partial void WorkflowLockLost(ILogger logger, Guid workflowId, string workflowType, string nodeId);

        [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "A WorkflowClaimed event handler failed for workflow {WorkflowId} - the workflow is still executed")]
        public static partial void WorkflowClaimedHandlerFailed(ILogger logger, Exception exception, Guid workflowId);

        [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "Node {NodeId} failed to claim workflows - no more workflows are claimed by this call")]
        public static partial void ClaimFailed(ILogger logger, Exception exception, string nodeId);

        [LoggerMessage(EventId = 1008, Level = LogLevel.Error, Message = "Workflow {WorkflowId} ({WorkflowType}) could not be executed - no more workflows are claimed by this call")]
        public static partial void ExecutionFailed(ILogger logger, Exception exception, Guid workflowId, string workflowType);
    }
}
