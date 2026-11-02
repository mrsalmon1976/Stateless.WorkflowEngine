using Stateless.WorkflowEngine.Events;
using Stateless.WorkflowEngine.Exceptions;
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
        private readonly ConcurrentDictionary<Guid, byte> _inFlight = new ConcurrentDictionary<Guid, byte>();
        private readonly object _startLock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _loopTask;
        private long _lastHeartbeatTicks;
        private volatile bool _isStarted;
        private volatile bool _isStopping;
        private volatile string _nodeId;

        public WorkflowNodeMonitor(IWorkflowStore workflowStore, WorkflowServerOptions options)
        {
            _workflowStore = workflowStore;
            _options = options;
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
        /// Marks a claimed workflow as being executed by the node.
        /// </summary>
        public void Track(Guid workflowId)
        {
            _inFlight[workflowId] = 0;
        }

        /// <summary>
        /// Marks a claimed workflow as no longer being executed by the node.
        /// </summary>
        public void Untrack(Guid workflowId)
        {
            _inFlight.TryRemove(workflowId, out _);
        }

        /// <summary>
        /// Runs one iteration of the monitor: heartbeat, removal of stale nodes, release of orphaned locks and release of
        /// this node's own stale locks.  Each step is independent, and failures are reported through NodeMonitorError.
        /// </summary>
        internal async Task TickAsync()
        {
            await RunStepAsync(HeartbeatAsync);
            await RunStepAsync(() => _workflowStore.RemoveStaleNodesAsync(_options.NodeTimeout));
            await RunStepAsync(ReleaseOrphanedLocksAsync);
            await RunStepAsync(ReleaseOwnStaleLocksAsync);
        }

        /// <summary>
        /// Stops claiming, waits (up to the node timeout) for in-flight workflows to finish, then releases the node's
        /// locks and removes the node so its workflows are available immediately.
        /// </summary>
        public void Dispose()
        {
            if (!_isStarted || _isStopping)
            {
                return;
            }
            _isStopping = true;

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    _loopTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // the loop handles its own errors - nothing to do on shutdown
                }
                _cancellationTokenSource.Dispose();
            }

            DateTime waitUntil = DateTime.UtcNow.Add(_options.NodeTimeout);
            while (!_inFlight.IsEmpty && DateTime.UtcNow < waitUntil)
            {
                Thread.Sleep(50);
            }

            try
            {
                _workflowStore.ReleaseLocks(_nodeId);
                _workflowStore.RemoveNode(_nodeId);
            }
            catch (Exception ex)
            {
                // if this fails, the node and its locks are removed by the other nodes after the node timeout
                OnNodeMonitorError(ex);
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

        private async Task RunStepAsync(Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                OnNodeMonitorError(ex);
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
        }

        private async Task HeartbeatAsync()
        {
            if (await _workflowStore.HeartbeatAsync(_nodeId))
            {
                Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
                return;
            }

            // the node record is gone, so another node considered this node dead and released its workflows - workflows
            // still executing keep the old id (their saves will fail if another node has claimed them), and new claims
            // use a new id
            string previousNodeId = _nodeId;
            string nodeId = Guid.NewGuid().ToString();
            Register(nodeId);
            _nodeId = nodeId;
            NodeEvicted?.Invoke(this, new NodeEvictedEventArgs(previousNodeId, nodeId));
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
            try
            {
                NodeMonitorError?.Invoke(this, new NodeMonitorErrorEventArgs(_nodeId, ex));
            }
            catch
            {
                // an event handler failing must not stop the monitor
            }
        }
    }
}
