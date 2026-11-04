using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stateless.WorkflowEngine.Events;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Logging;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.Services
{
    /// <summary>
    /// Registers a workflow server as a node in the workflow store, and runs a background loop that keeps the node's
    /// heartbeat up to date, removes nodes that have stopped heartbeating, and releases workflows locked by nodes that
    /// no longer exist.  Lock ownership is per node, so a workflow stays locked for as long as the node that claimed
    /// it is alive, no matter how long its action takes.
    /// </summary>
    internal class WorkflowNodeMonitor : IDisposable
    {
        private readonly IWorkflowStore _workflowStore;
        private readonly WorkflowServerOptions _options;
        private readonly ConcurrentDictionary<Guid, int> _inFlight = new ConcurrentDictionary<Guid, int>();
        private readonly object _inFlightLock = new object();
        private readonly object _startLock = new object();
        private readonly object _stopLock = new object();
        private readonly List<string> _previousNodeIds = new List<string>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _loopTask;
        private Task _stopTask;
        private int _claimsInProgress;
        private CancellationToken _shutdownToken;
        private CancellationTokenRegistration _shutdownRegistration;
        private long _lastHeartbeatTicks;
        private volatile bool _isStarted;
        private volatile bool _isStopping;
        private volatile string _nodeId;
        private readonly ILogger _logger;

        public WorkflowNodeMonitor(IWorkflowStore workflowStore, WorkflowServerOptions options, ILogger logger = null)
        {
            _workflowStore = workflowStore;
            _options = options;
            _logger = logger ?? NullLogger.Instance;
            _nodeId = String.IsNullOrWhiteSpace(options.NodeId) ? Guid.NewGuid().ToString() : options.NodeId;
        }

        /// <summary>
        /// Raised when the node finds it has been removed by another node, and has re-registered with a new id.
        /// </summary>
        public event EventHandler<NodeEvictedEventArgs> NodeEvicted;

        /// <summary>
        /// Raised when a heartbeat or monitoring operation fails.
        /// </summary>
        public event EventHandler<NodeMonitorErrorEventArgs> NodeMonitorError;

        /// <summary>
        /// Gets the current id of the node.  This changes if the node is evicted.
        /// </summary>
        public string NodeId => _nodeId;

        /// <summary>
        /// Gets whether the node is allowed to claim workflows.  A node stops claiming if it has not been able to heartbeat
        /// for half the node timeout, so it stops taking new work well before other nodes can consider it dead.
        /// </summary>
        public bool CanClaim
        {
            get
            {
                if (!_isStarted || _isStopping)
                {
                    return false;
                }
                DateTime lastHeartbeat = new DateTime(Interlocked.Read(ref _lastHeartbeatTicks), DateTimeKind.Utc);
                return DateTime.UtcNow - lastHeartbeat < TimeSpan.FromTicks(_options.NodeTimeout.Ticks / 2);
            }
        }

        /// <summary>
        /// Gets the ids of the workflows currently being executed by the node.
        /// </summary>
        public ICollection<Guid> InFlight => _inFlight.Keys;

        /// <summary>
        /// Registers the node and starts the background heartbeat loop, if not already started.  The node is registered
        /// before this returns, so it always exists before it claims any workflows.
        /// </summary>
        /// <param name="runLoop">False to register without starting the background loop (used for testing).</param>
        public void EnsureStarted(bool runLoop = true)
        {
            if (_isStarted)
            {
                return;
            }

            lock (_startLock)
            {
                if (_isStarted)
                {
                    return;
                }

                if (_options.HeartbeatInterval <= TimeSpan.Zero)
                {
                    throw new WorkflowException("HeartbeatInterval must be greater than zero");
                }
                if (_options.NodeTimeout < TimeSpan.FromTicks(_options.HeartbeatInterval.Ticks * 3))
                {
                    throw new WorkflowException("NodeTimeout must be at least 3 times the HeartbeatInterval");
                }

                Register(_nodeId);

                if (runLoop)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    CancellationToken token = _cancellationTokenSource.Token;
                    _loopTask = Task.Run(() => RunLoopAsync(token));
                }
                _isStarted = true;
            }
        }

        /// <summary>
        /// Sets the token that shuts the node down when cancelled (see <see cref="StopAsync"/>).  Only the most recent
        /// token is used, and a token that can't be cancelled is ignored.
        /// </summary>
        public void SetShutdownToken(CancellationToken token)
        {
            if (!token.CanBeCanceled)
            {
                return;
            }
            lock (_stopLock)
            {
                if (token == _shutdownToken)
                {
                    return;
                }
                _shutdownRegistration.Unregister();
                _shutdownToken = token;

                // StopAsync only starts the shutdown in the background, so cancelling the token doesn't block
                _shutdownRegistration = token.Register(() => this.StopAsync());
            }
        }

        /// <summary>
        /// Checks that the node can claim and, if so, records a claim in progress, which must be ended with
        /// <see cref="EndClaim"/> once the claimed workflows are tracked.  Shutdown waits for claims in progress, so
        /// workflows claimed as the node stops are never left locked.
        /// </summary>
        public bool TryBeginClaim()
        {
            lock (_stopLock)
            {
                if (!this.CanClaim)
                {
                    return false;
                }
                _claimsInProgress++;
                return true;
            }
        }

        /// <summary>
        /// Ends a claim started with <see cref="TryBeginClaim"/>.
        /// </summary>
        public void EndClaim()
        {
            lock (_stopLock)
            {
                _claimsInProgress--;
            }
        }

        /// <summary>
        /// Marks a claimed workflow as being executed by the node.  Tracking is counted: after an eviction the same
        /// workflow can still be executing under the old node id when it is claimed again under the new one, and it must
        /// stay in flight until both have finished.
        /// </summary>
        public void Track(Guid workflowId)
        {
            lock (_inFlightLock)
            {
                _inFlight[workflowId] = _inFlight.TryGetValue(workflowId, out int count) ? count + 1 : 1;
            }
        }

        /// <summary>
        /// Marks a claimed workflow as no longer being executed by the node.
        /// </summary>
        public void Untrack(Guid workflowId)
        {
            lock (_inFlightLock)
            {
                if (!_inFlight.TryGetValue(workflowId, out int count))
                {
                    return;
                }
                if (count > 1)
                {
                    _inFlight[workflowId] = count - 1;
                }
                else
                {
                    _inFlight.TryRemove(workflowId, out _);
                }
            }
        }

        /// <summary>
        /// Runs one iteration of the monitor: heartbeat, removal of stale nodes, release of orphaned locks and release of
        /// this node's own stale locks.  Each step is independent, and failures are reported through NodeMonitorError.
        /// </summary>
        internal async Task TickAsync()
        {
            await RunStepAsync(HeartbeatAsync);
            await RunStepAsync(RemoveStaleNodesAsync);
            await RunStepAsync(ReleaseOrphanedLocksAsync);
            await RunStepAsync(ReleaseOwnStaleLocksAsync);
        }

        /// <summary>
        /// Shuts the node down: stops claiming, waits (up to the node timeout) for claims in progress and in-flight
        /// workflows to finish, then releases the node's locks - including any held under ids it had before it was
        /// evicted - and removes the node so its workflows are available immediately.  The shutdown runs in the
        /// background, and calling this again returns the same task.  A node that has stopped can't be restarted.
        /// </summary>
        public Task StopAsync()
        {
            lock (_stopLock)
            {
                if (_stopTask == null)
                {
                    if (!_isStarted)
                    {
                        return Task.CompletedTask;
                    }
                    _isStopping = true;
                    _stopTask = Task.Run(StopCoreAsync);
                }
                return _stopTask;
            }
        }

        /// <summary>
        /// Shuts the node down (see <see cref="StopAsync"/>) and waits for the shutdown to finish.
        /// </summary>
        public void Dispose()
        {
            this.StopAsync().Wait();
            lock (_stopLock)
            {
                _shutdownRegistration.Unregister();
            }
        }

        private async Task StopCoreAsync()
        {
            if (_cancellationTokenSource != null)
            {
                // the loop handles its own errors, so it only needs a bounded wait
                _cancellationTokenSource.Cancel();
                await Task.WhenAny(_loopTask, Task.Delay(TimeSpan.FromSeconds(5)));
                _cancellationTokenSource.Dispose();
            }

            WorkflowNodeMonitorLog.NodeStopping(_logger, _nodeId);
            DateTime waitUntil = DateTime.UtcNow.Add(_options.NodeTimeout);
            while (!this.IsIdle() && DateTime.UtcNow < waitUntil)
            {
                await Task.Delay(50);
            }
            if (!this.IsIdle())
            {
                WorkflowNodeMonitorLog.StopTimedOut(_logger, _nodeId, _options.NodeTimeout, _inFlight.Count);
            }

            // if any of these fail, the node and its locks are removed by the other nodes after the node timeout
            List<string> nodeIds;
            lock (_stopLock)
            {
                nodeIds = _previousNodeIds.Concat(new[] { _nodeId }).ToList();
            }
            bool stopped = true;
            foreach (string nodeId in nodeIds)
            {
                stopped &= await RunStepAsync(() => _workflowStore.ReleaseLocksAsync(nodeId));
            }
            stopped &= await RunStepAsync(() => _workflowStore.RemoveNodeAsync(_nodeId));
            if (stopped)
            {
                WorkflowNodeMonitorLog.NodeStopped(_logger, _nodeId);
            }
        }

        private bool IsIdle()
        {
            lock (_stopLock)
            {
                return _claimsInProgress == 0 && _inFlight.IsEmpty;
            }
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            using (PeriodicTimer timer = new PeriodicTimer(_options.HeartbeatInterval))
            {
                try
                {
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        await TickAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    // stopping
                }
            }
        }

        /// <summary>
        /// Runs a monitor step, reporting a failure through NodeMonitorError.
        /// </summary>
        /// <returns>False if the step failed.</returns>
        private async Task<bool> RunStepAsync(Func<Task> step)
        {
            try
            {
                await step();
                return true;
            }
            catch (Exception ex)
            {
                OnNodeMonitorError(ex);
                return false;
            }
        }

        private void Register(string nodeId)
        {
            _workflowStore.RegisterNode(new WorkflowNode
            {
                Id = nodeId,
                Name = _options.NodeName,
                StartedOn = DateTime.UtcNow
            });
            Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
            WorkflowNodeMonitorLog.NodeRegistered(_logger, nodeId, _options.NodeName);
        }

        private async Task RegisterAsync(string nodeId)
        {
            await _workflowStore.RegisterNodeAsync(new WorkflowNode
            {
                Id = nodeId,
                Name = _options.NodeName,
                StartedOn = DateTime.UtcNow
            });
            Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
        }

        private async Task HeartbeatAsync()
        {
            if (await _workflowStore.HeartbeatAsync(_nodeId))
            {
                Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
                WorkflowNodeMonitorLog.Heartbeat(_logger, _nodeId);
                return;
            }

            // the node record is gone, so another node considered this node dead and released its workflows - workflows
            // still executing keep the old id (their saves will fail if another node has claimed them), and new claims
            // use a new id
            string previousNodeId = _nodeId;
            string nodeId = Guid.NewGuid().ToString();
            await RegisterAsync(nodeId);
            lock (_stopLock)
            {
                _previousNodeIds.Add(previousNodeId);
            }
            _nodeId = nodeId;
            WorkflowNodeMonitorLog.NodeEvicted(_logger, previousNodeId, nodeId);
            NodeEvicted?.Invoke(this, new NodeEvictedEventArgs(previousNodeId, nodeId));
        }

        private async Task RemoveStaleNodesAsync()
        {
            long removed = await _workflowStore.RemoveStaleNodesAsync(_options.NodeTimeout);
            if (removed > 0)
            {
                WorkflowNodeMonitorLog.StaleNodesRemoved(_logger, removed, _options.NodeTimeout);
            }
        }

        private async Task ReleaseOrphanedLocksAsync()
        {
            // the lock owners MUST be read before the nodes: a node always registers before it claims a workflow, so any
            // owner in this list was registered before the list was read, and will be in the node list unless it has
            // since been removed
            List<string> owners = (await _workflowStore.GetLockOwnersAsync()).Where(x => !String.IsNullOrEmpty(x)).ToList();
            if (owners.Count == 0)
            {
                return;
            }

            HashSet<string> liveNodes = new HashSet<string>((await _workflowStore.GetNodesAsync()).Select(x => x.Id));
            foreach (string owner in owners.Where(x => !liveNodes.Contains(x)))
            {
                await _workflowStore.ReleaseLocksAsync(owner);
                WorkflowNodeMonitorLog.OrphanedLocksReleased(_logger, owner);
            }
        }

        private async Task ReleaseOwnStaleLocksAsync()
        {
            // locks held by this node on workflows it is no longer executing (e.g. a save failed, or a claim succeeded on
            // the server but the response was lost) would otherwise never be released while the node is alive.  Only
            // locks older than the node timeout are considered, so a workflow that has just been claimed but not yet
            // tracked is never released.
            DateTime lockedBefore = DateTime.UtcNow.Subtract(_options.NodeTimeout);
            await _workflowStore.ReleaseStaleLocksAsync(_nodeId, lockedBefore, _inFlight.Keys.ToList());
        }

        private void OnNodeMonitorError(Exception ex)
        {
            WorkflowNodeMonitorLog.MonitorError(_logger, ex, _nodeId);
            try
            {
                NodeMonitorError?.Invoke(this, new NodeMonitorErrorEventArgs(_nodeId, ex));
            }
            catch (Exception handlerException)
            {
                // an event handler failing must not stop the monitor
                WorkflowNodeMonitorLog.MonitorErrorHandlerFailed(_logger, handlerException, _nodeId);
            }
        }
    }
}
