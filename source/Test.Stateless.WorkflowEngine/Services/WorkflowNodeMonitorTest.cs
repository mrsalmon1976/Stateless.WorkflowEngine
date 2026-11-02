using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Events;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Workflows.Basic;

namespace Test.Stateless.WorkflowEngine.Services
{
    [TestFixture]
    public class WorkflowNodeMonitorTest
    {
        private MemoryWorkflowStore _workflowStore;
        private WorkflowServerOptions _options;

        [SetUp]
        public void WorkflowNodeMonitorTest_SetUp()
        {
            _workflowStore = new MemoryWorkflowStore();
            _options = new WorkflowServerOptions()
            {
                HeartbeatInterval = TimeSpan.FromSeconds(1),
                NodeTimeout = TimeSpan.FromSeconds(3)
            };
        }

        #region EnsureStarted Tests

        [Test]
        public void EnsureStarted_OnStart_RegistersNode()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);

            WorkflowNode node = _workflowStore.GetNodes().Single();
            Assert.That(node.Id, Is.EqualTo(monitor.NodeId));
            Assert.That(node.Name, Is.EqualTo(Environment.MachineName));
            Assert.That(monitor.CanClaim, Is.True);
        }

        [Test]
        public void EnsureStarted_CalledTwice_RegistersOnce()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            monitor.EnsureStarted(runLoop: false);

            workflowStore.Received(1).RegisterNode(Arg.Any<WorkflowNode>());
        }

        [Test]
        public void EnsureStarted_HeartbeatIntervalZero_ThrowsException()
        {
            _options.HeartbeatInterval = TimeSpan.Zero;
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            Assert.Throws<WorkflowException>(() => monitor.EnsureStarted(runLoop: false));
        }

        [Test]
        public void CanClaim_NotStarted_ReturnsFalse()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            Assert.That(monitor.CanClaim, Is.False);
        }

        [Test]
        public async Task CanClaim_HeartbeatFailingForHalfTheTimeout_ReturnsFalse()
        {
            _options.HeartbeatInterval = TimeSpan.FromMilliseconds(50);
            _options.NodeTimeout = TimeSpan.FromMilliseconds(200);
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.HeartbeatAsync(Arg.Any<string>()).Returns<Task<bool>>(x => throw new TimeoutException("database unavailable"));

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(workflowStore, _options);
            List<Exception> errors = new List<Exception>();
            monitor.NodeMonitorError += (sender, e) => errors.Add(e.Exception);
            monitor.EnsureStarted(runLoop: false);
            Assert.That(monitor.CanClaim, Is.True);

            await Task.Delay(150);
            await monitor.TickAsync();

            Assert.That(monitor.CanClaim, Is.False);
            Assert.That(errors.OfType<TimeoutException>().Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task CanClaim_HeartbeatSucceeds_ReturnsTrue()
        {
            _options.HeartbeatInterval = TimeSpan.FromMilliseconds(50);
            _options.NodeTimeout = TimeSpan.FromMilliseconds(200);
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);

            await Task.Delay(150);
            await monitor.TickAsync();

            Assert.That(monitor.CanClaim, Is.True);
        }

        #endregion

        #region TickAsync Tests

        [Test]
        public async Task TickAsync_NodeStoppedHeartbeating_NodeRemovedAndItsWorkflowsReleased()
        {
            WorkflowNode deadNode = RegisterNode("dead", DateTime.UtcNow.AddMinutes(-5));
            BasicWorkflow workflow = SaveWorkflow();
            _workflowStore.ClaimNext(deadNode.Id);

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            await monitor.TickAsync();

            Assert.That(_workflowStore.GetNodes().Select(x => x.Id), Is.EqualTo(new[] { monitor.NodeId }));
            Assert.That(_workflowStore.GetLockOwners(), Is.Empty);
            Assert.That(_workflowStore.ClaimNext("another")?.Id, Is.EqualTo(workflow.Id));
        }

        [Test]
        public async Task TickAsync_NodeIsAlive_ItsWorkflowsStayLocked()
        {
            WorkflowNode liveNode = RegisterNode("alive", DateTime.UtcNow);
            SaveWorkflow();
            _workflowStore.ClaimNext(liveNode.Id);

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            await monitor.TickAsync();

            Assert.That(_workflowStore.GetNodes().Count(), Is.EqualTo(2));
            Assert.That(_workflowStore.GetLockOwners(), Is.EqualTo(new[] { liveNode.Id }));
        }

        [Test]
        public async Task TickAsync_WorkflowLockedByNodeThatDoesNotExist_WorkflowReleased()
        {
            SaveWorkflow();
            _workflowStore.ClaimNext("ghost");

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            await monitor.TickAsync();

            Assert.That(_workflowStore.GetLockOwners(), Is.Empty);
        }

        [Test]
        public async Task TickAsync_LockOwnersReadBeforeNodes_NodeRegisteredInBetweenIsNotReleased()
        {
            // a node that registers and claims between the two reads must not be treated as an orphan - this is only
            // safe because the lock owners are read before the nodes
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            List<string> calls = new List<string>();
            workflowStore.GetLockOwnersAsync().Returns(x => { calls.Add("owners"); return new[] { "new-node" }; });
            workflowStore.GetNodesAsync().Returns(x => { calls.Add("nodes"); return new[] { new WorkflowNode() { Id = "new-node" } }; });
            workflowStore.HeartbeatAsync(Arg.Any<string>()).Returns(true);

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            await monitor.TickAsync();

            Assert.That(calls, Is.EqualTo(new[] { "owners", "nodes" }));
            await workflowStore.DidNotReceive().ReleaseLocksAsync(Arg.Any<string>());
        }

        [Test]
        public async Task TickAsync_NodeWasRemoved_ReregistersWithNewIdAndRaisesEvent()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            List<NodeEvictedEventArgs> events = new List<NodeEvictedEventArgs>();
            monitor.NodeEvicted += (sender, e) => events.Add(e);
            monitor.EnsureStarted(runLoop: false);
            string originalNodeId = monitor.NodeId;

            _workflowStore.RemoveNode(originalNodeId);
            await monitor.TickAsync();

            Assert.That(monitor.NodeId, Is.Not.EqualTo(originalNodeId));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].PreviousNodeId, Is.EqualTo(originalNodeId));
            Assert.That(events[0].NodeId, Is.EqualTo(monitor.NodeId));
            Assert.That(_workflowStore.GetNodes().Select(x => x.Id), Is.EqualTo(new[] { monitor.NodeId }));
            Assert.That(monitor.CanClaim, Is.True);
        }

        [Test]
        public async Task TickAsync_OwnOldLockNotInFlight_Released()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);

            SaveWorkflow();
            Workflow claimed = _workflowStore.ClaimNext(monitor.NodeId);
            claimed.LockedOn = DateTime.UtcNow.AddHours(-1);

            await monitor.TickAsync();

            Assert.That(_workflowStore.GetLockOwners(), Is.Empty);
        }

        [Test]
        public async Task TickAsync_OwnOldLockInFlight_NotReleased()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);

            SaveWorkflow();
            Workflow claimed = _workflowStore.ClaimNext(monitor.NodeId);
            claimed.LockedOn = DateTime.UtcNow.AddHours(-1);
            monitor.Track(claimed.Id);

            await monitor.TickAsync();

            Assert.That(_workflowStore.GetLockOwners(), Is.EqualTo(new[] { monitor.NodeId }));
        }

        [Test]
        public async Task TickAsync_OwnRecentLockNotInFlight_NotReleased()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);

            // claimed, but not yet tracked
            SaveWorkflow();
            _workflowStore.ClaimNext(monitor.NodeId);

            await monitor.TickAsync();

            Assert.That(_workflowStore.GetLockOwners(), Is.EqualTo(new[] { monitor.NodeId }));
        }

        [Test]
        public async Task TickAsync_StepFails_OtherStepsStillRun()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.HeartbeatAsync(Arg.Any<string>()).Returns<Task<bool>>(x => throw new TimeoutException());
            workflowStore.GetLockOwnersAsync().Returns(new[] { "ghost" });
            workflowStore.GetNodesAsync().Returns(new WorkflowNode[0]);

            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            await monitor.TickAsync();

            await workflowStore.Received(1).RemoveStaleNodesAsync(_options.NodeTimeout);
            await workflowStore.Received(1).ReleaseLocksAsync("ghost");
        }

        #endregion

        #region Background Loop Tests

        [Test]
        public void EnsureStarted_LoopRunning_HeartbeatsAndReleasesDeadNodes()
        {
            _options.HeartbeatInterval = TimeSpan.FromMilliseconds(50);
            _options.NodeTimeout = TimeSpan.FromMilliseconds(500);
            WorkflowNode deadNode = RegisterNode("dead", DateTime.UtcNow.AddMinutes(-5));
            SaveWorkflow();
            _workflowStore.ClaimNext(deadNode.Id);

            using (WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options))
            {
                monitor.EnsureStarted();
                DateTime registeredHeartbeat = _workflowStore.GetNodes().Single(x => x.Id == monitor.NodeId).LastHeartbeat;

                DateTime timeout = DateTime.UtcNow.AddSeconds(5);
                while ((_workflowStore.GetLockOwners().Any() || _workflowStore.GetNodes().Single(x => x.Id == monitor.NodeId).LastHeartbeat == registeredHeartbeat) && DateTime.UtcNow < timeout)
                {
                    Thread.Sleep(20);
                }

                Assert.That(_workflowStore.GetLockOwners(), Is.Empty);
                Assert.That(_workflowStore.GetNodes().Select(x => x.Id), Is.EqualTo(new[] { monitor.NodeId }));
            }
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_NotStarted_DoesNothing()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(workflowStore, _options);
            monitor.Dispose();

            workflowStore.DidNotReceive().ReleaseLocks(Arg.Any<string>());
            workflowStore.DidNotReceive().RemoveNode(Arg.Any<string>());
        }

        [Test]
        public void Dispose_Started_ReleasesLocksAndRemovesNode()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted();
            SaveWorkflow();
            _workflowStore.ClaimNext(monitor.NodeId);

            monitor.Dispose();

            Assert.That(_workflowStore.GetLockOwners(), Is.Empty);
            Assert.That(_workflowStore.GetNodes(), Is.Empty);
            Assert.That(monitor.CanClaim, Is.False);
        }

        [Test]
        public void Dispose_WorkflowInFlight_WaitsForItToFinish()
        {
            WorkflowNodeMonitor monitor = new WorkflowNodeMonitor(_workflowStore, _options);
            monitor.EnsureStarted(runLoop: false);
            Guid workflowId = Guid.NewGuid();
            monitor.Track(workflowId);

            DateTime untrackedOn = DateTime.MinValue;
            Task.Run(async () =>
            {
                await Task.Delay(300);
                untrackedOn = DateTime.UtcNow;
                monitor.Untrack(workflowId);
            });

            monitor.Dispose();

            Assert.That(untrackedOn, Is.Not.EqualTo(DateTime.MinValue));
            Assert.That(_workflowStore.GetNodes(), Is.Empty);
        }

        #endregion

        #region Private Methods

        private WorkflowNode RegisterNode(string id, DateTime lastHeartbeat)
        {
            WorkflowNode node = new WorkflowNode() { Id = id, Name = id };
            _workflowStore.RegisterNode(node);

            // the memory store keeps the instance, so the heartbeat can be backdated
            node.LastHeartbeat = lastHeartbeat;
            return node;
        }

        private BasicWorkflow SaveWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            _workflowStore.Save(workflow);
            return workflow;
        }

        #endregion
    }
}
