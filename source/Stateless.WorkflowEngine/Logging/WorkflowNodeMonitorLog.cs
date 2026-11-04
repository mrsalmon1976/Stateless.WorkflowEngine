using Microsoft.Extensions.Logging;
using System;

namespace Stateless.WorkflowEngine.Logging
{
    /// <summary>
    /// Log messages written by the node monitor (event ids 1100-1199).
    /// </summary>
    internal static partial class WorkflowNodeMonitorLog
    {
        [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Node {NodeId} ({NodeName}) registered")]
        public static partial void NodeRegistered(ILogger logger, string nodeId, string nodeName);

        [LoggerMessage(EventId = 1101, Level = LogLevel.Debug, Message = "Node {NodeId} heartbeat")]
        public static partial void Heartbeat(ILogger logger, string nodeId);

        [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "Node {PreviousNodeId} was removed by another node, and has re-registered as {NodeId}")]
        public static partial void NodeEvicted(ILogger logger, string previousNodeId, string nodeId);

        [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "Removed {Count} node(s) that had not heartbeated within {NodeTimeout}")]
        public static partial void StaleNodesRemoved(ILogger logger, long count, TimeSpan nodeTimeout);

        [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "Released the workflows locked by node {OwnerNodeId}, which no longer exists")]
        public static partial void OrphanedLocksReleased(ILogger logger, string ownerNodeId);

        [LoggerMessage(EventId = 1105, Level = LogLevel.Error, Message = "Node {NodeId} monitor operation failed")]
        public static partial void MonitorError(ILogger logger, Exception exception, string nodeId);

        [LoggerMessage(EventId = 1106, Level = LogLevel.Warning, Message = "A NodeMonitorError event handler failed for node {NodeId}")]
        public static partial void MonitorErrorHandlerFailed(ILogger logger, Exception exception, string nodeId);

        [LoggerMessage(EventId = 1107, Level = LogLevel.Information, Message = "Node {NodeId} stopping - waiting for claims and running workflows to finish")]
        public static partial void NodeStopping(ILogger logger, string nodeId);

        [LoggerMessage(EventId = 1108, Level = LogLevel.Warning, Message = "Node {NodeId} stopped waiting after {NodeTimeout} with {InFlightCount} workflow(s) still running - their locks are released anyway")]
        public static partial void StopTimedOut(ILogger logger, string nodeId, TimeSpan nodeTimeout, int inFlightCount);

        [LoggerMessage(EventId = 1109, Level = LogLevel.Information, Message = "Node {NodeId} stopped: its locks have been released and the node removed")]
        public static partial void NodeStopped(ILogger logger, string nodeId);
    }
}
