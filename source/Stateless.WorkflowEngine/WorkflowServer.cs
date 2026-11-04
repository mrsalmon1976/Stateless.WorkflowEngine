using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System.Threading.Tasks;
using Stateless.WorkflowEngine.Services;
using Stateless.WorkflowEngine.Events;
using System.Threading;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stateless.WorkflowEngine.Logging;

namespace Stateless.WorkflowEngine
{
    public class WorkflowServer : IWorkflowServer
    {
        private readonly object _nodeMonitorLock = new object();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private WorkflowNodeMonitor _nodeMonitor;

        public WorkflowServer(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowServerOptions())
        {
        }

        public WorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions options) : this(workflowStore, options, null)
        {
        }

        /// <summary>
        /// Creates a workflow server that logs through the given logger factory.
        /// </summary>
        /// <param name="workflowStore"></param>
        /// <param name="options">The server options, or null for the defaults.</param>
        /// <param name="loggerFactory">The logger factory, or null to not log.</param>
        public WorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions options, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<WorkflowServer>();
            this.WorkflowStore = workflowStore;
            this.Options = options ?? new WorkflowServerOptions();
            this.WorkflowRegistrationService = new WorkflowRegistrationService();
            this.WorkflowExceptionHandler = new WorkflowExceptionHandler();

            workflowStore.Initialise(this.Options.AutoCreateTables, this.Options.AutoCreateIndexes, this.Options.PersistWorkflowDefinitions);
        }

        /// <summary>
        /// Event raised when a workflow is suspended.
        /// </summary>
        public event EventHandler<WorkflowEventArgs> WorkflowSuspended;

        /// <summary>
        /// Event raised when a workflow completes.
        /// </summary>
        public event EventHandler<WorkflowEventArgs> WorkflowCompleted;

        /// <summary>
        /// Event raised when a workflow executed by this server could not be saved because another node has claimed it
        /// since (or it no longer exists).  The workflow is not saved, and execution of it stops.
        /// </summary>
        public event EventHandler<WorkflowLockLostEventArgs> WorkflowLockLost;

        /// <summary>
        /// Event raised when this server claims a workflow for execution, just before it is executed.  Exceptions thrown by
        /// handlers are ignored, as the workflow is already locked and must still be executed.
        /// </summary>
        public event EventHandler<WorkflowClaimedEventArgs> WorkflowClaimed;

        /// <summary>
        /// Event raised when this server finds that it was removed by another node (it could not heartbeat within the
        /// node timeout), and has re-registered with a new node id.
        /// </summary>
        public event EventHandler<NodeEvictedEventArgs> NodeEvicted;

        /// <summary>
        /// Event raised when the background heartbeat/monitoring of this server fails.
        /// </summary>
        public event EventHandler<NodeMonitorErrorEventArgs> NodeMonitorError;

        /// <summary>
        /// Gets/sets the workflow registration service
        /// </summary>
        internal IWorkflowRegistrationService WorkflowRegistrationService { get; set; }

        /// <summary>
        /// Gets/sets the workflow registration service
        /// </summary>
        internal IWorkflowExceptionHandler WorkflowExceptionHandler { get; set; }

        /// <summary>
        /// Gets the monitor that registers this server as a node and keeps its heartbeat up to date.  It is created on
        /// first use, and only registers the node once workflows are executed.
        /// </summary>
        internal WorkflowNodeMonitor NodeMonitor
        {
            get
            {
                if (_nodeMonitor == null)
                {
                    lock (_nodeMonitorLock)
                    {
                        if (_nodeMonitor == null)
                        {
                            WorkflowNodeMonitor nodeMonitor = new WorkflowNodeMonitor(this.WorkflowStore, this.Options, _loggerFactory.CreateLogger<WorkflowNodeMonitor>());
                            nodeMonitor.NodeEvicted += (sender, e) => this.NodeEvicted?.Invoke(this, e);
                            nodeMonitor.NodeMonitorError += (sender, e) => this.NodeMonitorError?.Invoke(this, e);
                            _nodeMonitor = nodeMonitor;
                        }
                    }
                }
                return _nodeMonitor;
            }
        }

        /// <summary>
        /// Gets/sets the resolver used to instantiate new instances of classes required for workflow execution.
        /// This defaults to null, in which case classes are created with reflection.  Setting this property
        /// to your own resolver will allow you to control how workflow actions are created.
        /// </summary>
        public IWorkflowEngineDependencyResolver DependencyResolver { get; set; }

        /// <summary>
        /// Gets the id of this server's node, used to lock the workflows it claims.  This changes if the node is evicted.
        /// </summary>
        public string NodeId => this.NodeMonitor.NodeId;

        /// <summary>
        /// Gets/sets the options applicable to the workflow server.
        /// </summary>
        public WorkflowServerOptions Options { get; set; }

        /// <summary>
        /// Gets/sets the workflow store attached to the workflow server.
        /// </summary>
        public IWorkflowStore WorkflowStore { get; set; }

        /// <summary>
        /// Executes a workflow.  Note that the workflow is saved without checking whether it is locked by another node -
        /// use ExecuteWorkflows to execute workflows when more than one workflow server uses the same store.
        /// </summary>
        /// <param name="workflow"></param>
        public void ExecuteWorkflow(Workflow workflow)
        {
            this.ExecuteWorkflow(workflow, null);
        }

        /// <summary>
        /// Executes a workflow.  Note that the workflow is saved without checking whether it is locked by another node -
        /// use ExecuteWorkflowsAsync to execute workflows when more than one workflow server uses the same store.
        /// </summary>
        /// <param name="workflow"></param>
        public async Task ExecuteWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            await this.ExecuteWorkflowAsync(workflow, null, cancellationToken);
        }

        /// <summary>
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        /// <param name="ownerNodeId">The node that claimed the workflow, or null if it was not claimed.  Saves of
        /// claimed workflows only succeed while the workflow is not locked by another node.</param>
        private void ExecuteWorkflow(Workflow workflow, string ownerNodeId)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("Workflow server cannot execute null workflow object");
            }

            // set the dependency resolver on the workflow to allow for dependency injection
            workflow.DependencyResolver = this.DependencyResolver;
            int priorityBeforeFire = workflow.Priority;
            bool continueExecution = false;

            // if the workflow is marked as complete, don't attempt to run it
            if (!workflow.IsComplete)
            {
                string initialState = workflow.CurrentState;
                bool hasError = false;
                bool isSaved = false;
                try
                {
                    workflow.LastException = null;
                    WorkflowServerLog.ExecutingStep(_logger, workflow.Id, workflow.GetType().Name, initialState, workflow.ResumeTrigger);
                    workflow.Fire(workflow.ResumeTrigger);

                    workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
                }
                catch (Exception ex)
                {
                    hasError = true;
                    this.HandleExecutionError(workflow, initialState, ex);
                }
                finally
                {
                    // the workflow should always save, no matter what happens
                    continueExecution = !hasError && this.CanContinueExecution(workflow, priorityBeforeFire);
                    isSaved = this.SaveWorkflow(workflow, ownerNodeId, keepLock: continueExecution || (!hasError && workflow.IsComplete));
                }

                // exit out on an error or if the workflow has been claimed by another node, nothing else to do here
                if (hasError || !isSaved)
                {
                    return;
                }
            }

            // if the workflow is complete, finish off - this is NOT an else (workflow coule have completed in the fire action)
            if (workflow.IsComplete)
            {
                workflow.CompletedOn = DateTime.UtcNow;
                if (!this.ArchiveWorkflow(workflow, ownerNodeId))
                {
                    return;
                }
                WorkflowServerLog.WorkflowCompleted(_logger, workflow.Id, workflow.GetType().Name);
                workflow.OnComplete();
                var completedHandler = this.WorkflowCompleted;
                if (completedHandler != null)
                {
                    completedHandler(this, new WorkflowEventArgs(workflow));
                }
                return;
            }

            // if the workflow is ready to resume immediately, just execute immediately instead of waiting for polling - saves database
            // hits - but if the priority has changed then rather allow for another poll
            if (continueExecution)
            {
                this.ExecuteWorkflow(workflow, ownerNodeId);
            }
        }

        /// <summary>
        /// Executes a workflow.
        /// </summary>
        /// <param name="workflow"></param>
        /// <param name="ownerNodeId">The node that claimed the workflow, or null if it was not claimed.  Saves of
        /// claimed workflows only succeed while the workflow is not locked by another node.</param>
        private async Task ExecuteWorkflowAsync(Workflow workflow, string ownerNodeId, CancellationToken cancellationToken)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException("Workflow server cannot execute null workflow object");
            }

            // set the dependency resolver on the workflow to allow for dependency injection
            workflow.DependencyResolver = this.DependencyResolver;
            int priorityBeforeFire = workflow.Priority;
            bool continueExecution = false;

            // if the workflow is marked as complete, don't attempt to run it
            if (!workflow.IsComplete)
            {
                string initialState = workflow.CurrentState;
                bool hasError = false;
                bool isSaved = false;
                try
                {
                    workflow.LastException = null;
                    WorkflowServerLog.ExecutingStep(_logger, workflow.Id, workflow.GetType().Name, initialState, workflow.ResumeTrigger);
                    await workflow.FireAsync(workflow.ResumeTrigger, cancellationToken);

                    workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
                }
                catch (Exception ex)
                {
                    hasError = true;
                    this.HandleExecutionError(workflow, initialState, ex);
                }
                finally
                {
                    // the workflow should always save, no matter what happens
                    continueExecution = !hasError && this.CanContinueExecution(workflow, priorityBeforeFire);
                    isSaved = await this.SaveWorkflowAsync(workflow, ownerNodeId, keepLock: continueExecution || (!hasError && workflow.IsComplete));
                }

                // exit out on an error or if the workflow has been claimed by another node, nothing else to do here
                if (hasError || !isSaved)
                {
                    return;
                }
            }

            // if the workflow is complete, finish off - this is NOT an else (workflow coule have completed in the fire action)
            if (workflow.IsComplete)
            {
                workflow.CompletedOn = DateTime.UtcNow;
                if (!await this.ArchiveWorkflowAsync(workflow, ownerNodeId))
                {
                    return;
                }
                WorkflowServerLog.WorkflowCompleted(_logger, workflow.Id, workflow.GetType().Name);
                workflow.OnComplete();
                var completedHandler = this.WorkflowCompleted;
                if (completedHandler != null)
                {
                    completedHandler(this, new WorkflowEventArgs(workflow));
                }
                return;
            }

            // if the workflow is ready to resume immediately, just execute immediately instead of waiting for polling - saves database
            // hits - but if the priority has changed then rather allow for another poll
            if (continueExecution)
            {
                await this.ExecuteWorkflowAsync(workflow, ownerNodeId, cancellationToken);
            }
        }

        /// <summary>
        /// Claims and executes up to <c>count</c> workflows from the registered store, ordered by Priority DESC, RetryCount
        /// DESC, and then by CreationDate.  The <c>maxConcurrent</c> execution slots share the workflows claimed by this
        /// call: batches of up to <c>Options.ClaimBatchSize</c> workflows are claimed in one store call, the next one in the
        /// background as soon as the slots have taken the previous one, so up to <c>ClaimBatchSize</c> claimed workflows can
        /// be waiting for a slot.  Multiple servers can safely execute workflows from the same store.
        /// </summary>
        /// <param name="count">The maximum number of workflows to execute.</param>
        /// <param name="maxConcurrent">The maximum number of workflows to processing parallel - defaults to the value of <c>count</c>.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        public int ExecuteWorkflows(int count, int? maxConcurrent = null)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count", "Count parameter must be greater than 0");
            }

            WorkflowNodeMonitor nodeMonitor = this.NodeMonitor;
            nodeMonitor.EnsureStarted();
            int concurrent = Math.Min(maxConcurrent ?? count, count);
            ClaimSlots slots = this.CreateClaimSlots(count);

            Task[] workers = Enumerable.Range(0, concurrent)
                .Select(_ => Task.Run(() => this.ExecuteClaimedWorkflows(nodeMonitor, slots)))
                .ToArray();
            Task.WaitAll(workers);

            slots.Error?.Throw();
            return slots.ClaimedCount;
        }

        /// <summary>
        /// Claims and executes up to <c>count</c> workflows from the registered store, ordered by Priority DESC, RetryCount
        /// DESC, and then by CreationDate.  The <c>maxConcurrent</c> execution slots share the workflows claimed by this
        /// call: batches of up to <c>Options.ClaimBatchSize</c> workflows are claimed in one store call, the next one in the
        /// background as soon as the slots have taken the previous one, so up to <c>ClaimBatchSize</c> claimed workflows can
        /// be waiting for a slot.  Multiple servers can safely execute workflows from the same store.
        /// </summary>
        /// <param name="count">The maximum number of workflows to execute.</param>
        /// <param name="maxConcurrent">The maximum number of workflows to processing parallel - defaults to the value of <c>count</c>.</param>
        /// <param name="cancellationToken">Stops claiming when cancelled (the workflows already claimed are still
        /// executed).  It also shuts the server's node down: the most recent token passed to this method is kept after
        /// it returns, and when it is cancelled the node releases its locks and is removed, as on <c>Dispose</c> - so
        /// pass the application's shutdown token, not a per-call timeout.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        public async Task<int> ExecuteWorkflowsAsync(int count, int? maxConcurrent = null, CancellationToken? cancellationToken = null)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count", "Count parameter must be greater than 0");
            }

            CancellationToken ct = cancellationToken ?? CancellationToken.None;
            WorkflowNodeMonitor nodeMonitor = this.NodeMonitor;
            nodeMonitor.EnsureStarted();
            nodeMonitor.SetShutdownToken(ct);
            int concurrent = Math.Min(maxConcurrent ?? count, count);
            ClaimSlots slots = this.CreateClaimSlots(count);

            // the token is not passed to Task.Run: a claimed workflow must always run (and save), or it would stay locked
            // until the node's stale locks are released - the slots check the token before each claim instead
            Task[] workers = Enumerable.Range(0, concurrent)
                .Select(_ => Task.Run(() => this.ExecuteClaimedWorkflowsAsync(nodeMonitor, slots, ct)))
                .ToArray();
            await Task.WhenAll(workers);

            slots.Error?.Throw();
            return slots.ClaimedCount;
        }

        /// <summary>
        /// Runs one execution slot: executes claimed workflows one after the other until the slots have claimed
        /// <c>count</c> workflows, there is nothing left to claim, or the node can no longer claim.  A workflow that fails
        /// (e.g. its save throws) stops claiming but not the slot, so every claimed workflow is still executed and
        /// untracked - a lock left behind is then released by the node's stale lock release.
        /// </summary>
        private void ExecuteClaimedWorkflows(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots)
        {
            ClaimedWorkflow claimed;
            while ((claimed = this.TakeClaimedWorkflow(nodeMonitor, slots)) != null)
            {
                try
                {
                    this.ExecuteWorkflow(claimed.Workflow, claimed.NodeId);
                }
                catch (Exception ex)
                {
                    WorkflowServerLog.ExecutionFailed(_logger, ex, claimed.Workflow.Id, claimed.Workflow.GetType().Name);
                    slots.ExecutionFailed(ex);
                }
                finally
                {
                    nodeMonitor.Untrack(claimed.Workflow.Id);
                }
            }
        }

        /// <summary>
        /// Runs one execution slot: executes claimed workflows one after the other until the slots have claimed
        /// <c>count</c> workflows, there is nothing left to claim, the node can no longer claim, or the token is cancelled.
        /// A workflow that fails (e.g. its save throws) stops claiming but not the slot, so every claimed workflow is still
        /// executed and untracked - a lock left behind is then released by the node's stale lock release.
        /// </summary>
        private async Task ExecuteClaimedWorkflowsAsync(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots, CancellationToken ct)
        {
            ClaimedWorkflow claimed;
            while ((claimed = await this.TakeClaimedWorkflowAsync(nodeMonitor, slots, ct)) != null)
            {
                try
                {
                    await this.ExecuteWorkflowAsync(claimed.Workflow, claimed.NodeId, ct);
                }
                catch (Exception ex)
                {
                    WorkflowServerLog.ExecutionFailed(_logger, ex, claimed.Workflow.Id, claimed.Workflow.GetType().Name);
                    slots.ExecutionFailed(ex);
                }
                finally
                {
                    nodeMonitor.Untrack(claimed.Workflow.Id);
                }
            }
        }

        /// <summary>
        /// Takes the next claimed workflow for a slot, waiting for the claim in flight if none are queued.  Returns null
        /// once claiming has stopped and all the claimed workflows have been taken.
        /// </summary>
        private ClaimedWorkflow TakeClaimedWorkflow(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots)
        {
            // a claim starts whenever canClaim returns true, and it ends the claim recorded by TryBeginClaim
            Func<bool> canClaim = () => nodeMonitor.TryBeginClaim();
            Func<int, Task> startClaim = batchSize => Task.Run(() => this.ClaimBatch(nodeMonitor, slots, batchSize));
            while (true)
            {
                if (slots.TryTake(canClaim, startClaim, out ClaimedWorkflow claimed, out Task claim))
                {
                    return claimed;
                }
                if (claim == null)
                {
                    return null;
                }
                // claims never fault - errors are recorded on the slots
                claim.Wait();
            }
        }

        /// <summary>
        /// Takes the next claimed workflow for a slot, waiting for the claim in flight if none are queued.  Returns null
        /// once claiming has stopped (or the token is cancelled) and all the claimed workflows have been taken - claimed
        /// workflows are always returned, so they are executed and released even if the token is cancelled.
        /// </summary>
        private async Task<ClaimedWorkflow> TakeClaimedWorkflowAsync(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots, CancellationToken ct)
        {
            // a claim starts whenever canClaim returns true, and it ends the claim recorded by TryBeginClaim
            Func<bool> canClaim = () => !ct.IsCancellationRequested && nodeMonitor.TryBeginClaim();
            Func<int, Task> startClaim = batchSize => Task.Run(() => this.ClaimBatchAsync(nodeMonitor, slots, batchSize));
            while (true)
            {
                if (slots.TryTake(canClaim, startClaim, out ClaimedWorkflow claimed, out Task claim))
                {
                    return claimed;
                }
                if (claim == null)
                {
                    return null;
                }
                // claims never fault - errors are recorded on the slots
                await claim;
            }
        }

        private ClaimSlots CreateClaimSlots(int count)
        {
            if (this.Options.ClaimBatchSize < 1)
            {
                throw new WorkflowException("ClaimBatchSize must be at least 1");
            }
            return new ClaimSlots(count, this.Options.ClaimBatchSize);
        }

        /// <summary>
        /// Claims a batch of workflows in the background and adds them to the slots.  The claim was started with
        /// <c>TryBeginClaim</c>, and is ended once the claimed workflows are tracked.
        /// </summary>
        private void ClaimBatch(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots, int batchSize)
        {
            try
            {
                string nodeId = nodeMonitor.NodeId;
                List<Workflow> workflows = this.WorkflowStore.ClaimNext(nodeId, batchSize).ToList();
                this.AddClaimed(nodeMonitor, slots, nodeId, workflows, batchSize);
            }
            catch (Exception ex)
            {
                // the slots finish the workflows they are running before the error is raised
                WorkflowServerLog.ClaimFailed(_logger, ex, nodeMonitor.NodeId);
                slots.ClaimFailed(ex, batchSize);
            }
            finally
            {
                nodeMonitor.EndClaim();
            }
        }

        /// <summary>
        /// Claims a batch of workflows in the background and adds them to the slots.  The claim was started with
        /// <c>TryBeginClaim</c>, and is ended once the claimed workflows are tracked.
        /// </summary>
        private async Task ClaimBatchAsync(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots, int batchSize)
        {
            try
            {
                string nodeId = nodeMonitor.NodeId;
                List<Workflow> workflows = (await this.WorkflowStore.ClaimNextAsync(nodeId, batchSize)).ToList();
                this.AddClaimed(nodeMonitor, slots, nodeId, workflows, batchSize);
            }
            catch (Exception ex)
            {
                // the slots finish the workflows they are running before the error is raised
                WorkflowServerLog.ClaimFailed(_logger, ex, nodeMonitor.NodeId);
                slots.ClaimFailed(ex, batchSize);
            }
            finally
            {
                nodeMonitor.EndClaim();
            }
        }

        /// <summary>
        /// Tracks a claimed batch (so the node's stale lock release skips it), raises <c>WorkflowClaimed</c> for each
        /// workflow, and adds the batch to the slots.
        /// </summary>
        private void AddClaimed(WorkflowNodeMonitor nodeMonitor, ClaimSlots slots, string nodeId, List<Workflow> workflows, int batchSize)
        {
            if (workflows.Count > 0)
            {
                WorkflowServerLog.BatchClaimed(_logger, nodeId, workflows.Count);
            }
            foreach (Workflow workflow in workflows)
            {
                nodeMonitor.Track(workflow.Id);
                OnWorkflowClaimed(workflow, nodeId);
            }
            slots.ClaimCompleted(workflows.Select(x => new ClaimedWorkflow(x, nodeId)).ToList(), batchSize);
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsSingleInstanceWorkflowRegistered<T>() where T : Workflow
        {
            return this.WorkflowRegistrationService.IsSingleInstanceWorkflowRegistered<T>(this.WorkflowStore);
        }

        /// <summary>
        /// Checks to see if a single-instance workflow has already been registered.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<bool> IsSingleInstanceWorkflowRegisteredAsync<T>() where T : Workflow
        {
            return await this.WorkflowRegistrationService.IsSingleInstanceWorkflowRegisteredAsync<T>(this.WorkflowStore);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        /// <returns>True if a new workflow was started, otherwise false.</returns>
        public void RegisterWorkflow(Workflow workflow)
        {
            this.WorkflowRegistrationService.RegisterWorkflow(this.WorkflowStore, workflow);
        }

        /// <summary>
        /// Registers a new workflow with the engine.  Single instance workflows that already exist will result in
        /// an exception being raised.
        /// </summary>
        /// <param name="workflow">The workflow.</param>
        public async Task RegisterWorkflowAsync(Workflow workflow)
        {
            await this.WorkflowRegistrationService.RegisterWorkflowAsync(this.WorkflowStore, workflow);
        }

        /// <summary>
        /// Registers a workflow type for processing.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterWorkflowType<T>() where T : Workflow
        {
            this.WorkflowStore.RegisterType(typeof(T));

            if (this.Options.PersistWorkflowDefinitions)
            {
                try
                {
                    Type workflowType = typeof(T);
                    Workflow workflow = Activator.CreateInstance(workflowType) as Workflow;
                    string graph = workflow.GetGraph();

                    // check to see if the definition already exists
                    WorkflowDefinition workflowDefinition = this.WorkflowStore.GetDefinitionByQualifiedName(workflowType.FullName);
                    if (workflowDefinition == null)
                    {
                        workflowDefinition = new WorkflowDefinition();
                    }
                    workflowDefinition.Name = workflowType.Name;
                    workflowDefinition.QualifiedName = workflowType.FullName;
                    workflowDefinition.Graph = graph;
                    workflowDefinition.LastUpdatedUtc = DateTime.UtcNow;
                    this.WorkflowStore.SaveDefinition(workflowDefinition);
                }
                catch (Exception ex)
                {
                    throw new WorkflowException("Unable to generate workflow definition graph - ensure the WorkflowDefinitions configuration is correct or remove definitions from server options", ex);
                }
            }
        }

        /// <summary>
        /// Stops the server from claiming workflows, waits (up to the node timeout) for workflows it is claiming or
        /// executing to finish, and then releases its locks and removes its node so that other servers can pick up its
        /// workflows immediately.  Call this when the application shuts down - this also happens when the token last
        /// passed to <c>ExecuteWorkflowsAsync</c> is cancelled, in which case this waits for that shutdown to finish.
        /// </summary>
        public void Dispose()
        {
            _nodeMonitor?.Dispose();
        }

        /// <summary>
        /// Checks whether a workflow that has just been fired can be executed again immediately.
        /// </summary>
        private bool CanContinueExecution(Workflow workflow, int priorityBeforeFire)
        {
            return !workflow.IsComplete
                && !workflow.IsSuspended
                && !String.IsNullOrWhiteSpace(workflow.ResumeTrigger)
                && workflow.ResumeOn <= DateTime.UtcNow
                && workflow.Priority == priorityBeforeFire;
        }

        /// <summary>
        /// Saves a workflow.  Claimed workflows are released in the same write unless <c>keepLock</c> is true (the
        /// workflow will continue executing or be archived), and are only saved if they are not locked by another node.
        /// </summary>
        /// <returns>False if the workflow has been claimed by another node.</returns>
        private bool SaveWorkflow(Workflow workflow, string ownerNodeId, bool keepLock)
        {
            if (ownerNodeId == null)
            {
                this.WorkflowStore.Save(workflow);
                return true;
            }

            if (!keepLock)
            {
                ClearLock(workflow);
            }
            if (this.WorkflowStore.SaveClaimed(workflow, ownerNodeId))
            {
                return true;
            }
            OnWorkflowLockLost(workflow, ownerNodeId);
            return false;
        }

        /// <summary>
        /// Saves a workflow.  Claimed workflows are released in the same write unless <c>keepLock</c> is true (the
        /// workflow will continue executing or be archived), and are only saved if they are not locked by another node.
        /// </summary>
        /// <returns>False if the workflow has been claimed by another node.</returns>
        private async Task<bool> SaveWorkflowAsync(Workflow workflow, string ownerNodeId, bool keepLock)
        {
            if (ownerNodeId == null)
            {
                await this.WorkflowStore.SaveAsync(workflow);
                return true;
            }

            if (!keepLock)
            {
                ClearLock(workflow);
            }
            if (await this.WorkflowStore.SaveClaimedAsync(workflow, ownerNodeId))
            {
                return true;
            }
            OnWorkflowLockLost(workflow, ownerNodeId);
            return false;
        }

        /// <summary>
        /// Archives a completed workflow.  Claimed workflows are only archived if they are not locked by another node.
        /// </summary>
        /// <returns>False if the workflow has been claimed by another node.</returns>
        private bool ArchiveWorkflow(Workflow workflow, string ownerNodeId)
        {
            if (ownerNodeId == null)
            {
                this.WorkflowStore.Archive(workflow);
                return true;
            }

            ClearLock(workflow);
            if (this.WorkflowStore.ArchiveClaimed(workflow, ownerNodeId))
            {
                return true;
            }
            OnWorkflowLockLost(workflow, ownerNodeId);
            return false;
        }

        /// <summary>
        /// Archives a completed workflow.  Claimed workflows are only archived if they are not locked by another node.
        /// </summary>
        /// <returns>False if the workflow has been claimed by another node.</returns>
        private async Task<bool> ArchiveWorkflowAsync(Workflow workflow, string ownerNodeId)
        {
            if (ownerNodeId == null)
            {
                await this.WorkflowStore.ArchiveAsync(workflow);
                return true;
            }

            ClearLock(workflow);
            if (await this.WorkflowStore.ArchiveClaimedAsync(workflow, ownerNodeId))
            {
                return true;
            }
            OnWorkflowLockLost(workflow, ownerNodeId);
            return false;
        }

        private static void ClearLock(Workflow workflow)
        {
            workflow.LockedBy = null;
            workflow.LockedOn = null;
        }

        private void OnWorkflowClaimed(Workflow workflow, string nodeId)
        {
            try
            {
                this.WorkflowClaimed?.Invoke(this, new WorkflowClaimedEventArgs(workflow, nodeId));
            }
            catch (Exception ex)
            {
                // the workflow is locked by this node, so it must be executed (and released) whatever the handler does
                WorkflowServerLog.WorkflowClaimedHandlerFailed(_logger, ex, workflow.Id);
            }
        }

        /// <summary>
        /// Handles an error raised while firing a workflow: restores its state, sets it up for a retry (or suspends it),
        /// and raises the workflow's error and suspend hooks and the server's WorkflowSuspended event.
        /// </summary>
        private void HandleExecutionError(Workflow workflow, string initialState, Exception ex)
        {
            workflow.CurrentState = initialState;
            this.WorkflowExceptionHandler.HandleWorkflowException(workflow, ex);

            if (workflow.IsSuspended)
            {
                WorkflowServerLog.WorkflowSuspended(_logger, ex, workflow.Id, workflow.GetType().Name, initialState);
            }
            else
            {
                WorkflowServerLog.WorkflowRetrying(_logger, ex, workflow.Id, workflow.GetType().Name, initialState, workflow.RetryCount, workflow.ResumeOn);
            }

            // raise the exception handler
            workflow.OnError(ex);

            // if the workflow is suspended, raise the events
            if (workflow.IsSuspended)
            {
                workflow.OnSuspend();
                var suspendedHandler = this.WorkflowSuspended;
                if (suspendedHandler != null)
                {
                    suspendedHandler(this, new WorkflowEventArgs(workflow));
                }
            }
        }

        private void OnWorkflowLockLost(Workflow workflow, string ownerNodeId)
        {
            WorkflowServerLog.WorkflowLockLost(_logger, workflow.Id, workflow.GetType().Name, ownerNodeId);
            var lockLostHandler = this.WorkflowLockLost;
            if (lockLostHandler != null)
            {
                lockLostHandler(this, new WorkflowLockLostEventArgs(workflow, ownerNodeId));
            }
        }

        /// <summary>
        /// A workflow claimed by an <c>ExecuteWorkflows</c> call, and the node id it was claimed with.
        /// </summary>
        private sealed class ClaimedWorkflow
        {
            public ClaimedWorkflow(Workflow workflow, string nodeId)
            {
                this.Workflow = workflow;
                this.NodeId = nodeId;
            }

            public Workflow Workflow { get; }

            public string NodeId { get; }
        }

        /// <summary>
        /// State shared by the execution slots of one <c>ExecuteWorkflows</c> call: the claimed workflows waiting for a
        /// slot, the claim in flight, how many more may be claimed, and whether claiming has stopped.
        /// <para>
        /// At most one claim is in flight.  It is started when a slot takes the last queued workflow, so the next batch
        /// is claimed while the slots are still executing - or by a slot that finds nothing queued.  As a claim only
        /// starts once the queue is empty, no more than the batch size of claimed workflows wait for a slot.  A slot only
        /// stops once nothing is queued, no claim is in flight and no more can be claimed, so every claimed workflow is
        /// executed.
        /// </para>
        /// </summary>
        private sealed class ClaimSlots
        {
            private readonly object _sync = new object();
            private readonly Queue<ClaimedWorkflow> _claimed = new Queue<ClaimedWorkflow>();
            private readonly int _batchSize;
            private int _remaining;
            private int _claimedCount;
            private bool _stopped;
            private Task _claim;
            private ExceptionDispatchInfo _error;

            public ClaimSlots(int count, int batchSize)
            {
                _remaining = count;
                _batchSize = batchSize;
            }

            public int ClaimedCount
            {
                get
                {
                    lock (_sync)
                    {
                        return _claimedCount;
                    }
                }
            }

            /// <summary>
            /// Gets the first claim or execution error, raised once every slot has finished.
            /// </summary>
            public ExceptionDispatchInfo Error
            {
                get
                {
                    lock (_sync)
                    {
                        return _error;
                    }
                }
            }

            /// <summary>
            /// Takes the next claimed workflow, starting the next claim if this empties the queue.  If nothing is queued,
            /// returns false with the claim to wait for before trying again - or a null claim if the slot is finished.
            /// </summary>
            /// <param name="canClaim">Whether the node may claim (checked before each claim) - claiming stops if not.</param>
            /// <param name="startClaim">Starts a background claim of the given batch size, which must call
            /// <see cref="ClaimCompleted"/> or <see cref="ClaimFailed"/>, and must not complete synchronously.</param>
            public bool TryTake(Func<bool> canClaim, Func<int, Task> startClaim, out ClaimedWorkflow claimed, out Task claim)
            {
                lock (_sync)
                {
                    if (_claimed.Count > 0)
                    {
                        claimed = _claimed.Dequeue();
                        if (_claimed.Count == 0)
                        {
                            this.StartClaim(canClaim, startClaim);
                        }
                        claim = null;
                        return true;
                    }

                    claimed = null;
                    this.StartClaim(canClaim, startClaim);
                    claim = _claim;
                    return false;
                }
            }

            public void ClaimCompleted(List<ClaimedWorkflow> workflows, int batchSize)
            {
                lock (_sync)
                {
                    // the batch size was reserved when the claim started - give back what wasn't claimed
                    _remaining += batchSize - workflows.Count;
                    _claimedCount += workflows.Count;
                    if (workflows.Count == 0)
                    {
                        _stopped = true;
                    }
                    foreach (ClaimedWorkflow workflow in workflows)
                    {
                        _claimed.Enqueue(workflow);
                    }
                    _claim = null;
                }
            }

            public void ClaimFailed(Exception ex, int batchSize)
            {
                lock (_sync)
                {
                    _remaining += batchSize;
                    this.Fail(ex);
                    _claim = null;
                }
            }

            /// <summary>
            /// Records a workflow that failed to execute and stops claiming - the workflows already claimed are still
            /// executed.
            /// </summary>
            public void ExecutionFailed(Exception ex)
            {
                lock (_sync)
                {
                    this.Fail(ex);
                }
            }

            /// <summary>
            /// Records the first error and stops claiming.  Must be called while holding the lock.
            /// </summary>
            private void Fail(Exception ex)
            {
                if (_error == null)
                {
                    _error = ExceptionDispatchInfo.Capture(ex);
                }
                _stopped = true;
            }

            /// <summary>
            /// Starts a claim if none is in flight and more may be claimed.  Must be called while holding the lock - the
            /// claim can't complete before <c>_claim</c> is set, as completing needs the lock.
            /// </summary>
            private void StartClaim(Func<bool> canClaim, Func<int, Task> startClaim)
            {
                if (_claim != null || _stopped || _remaining <= 0)
                {
                    return;
                }
                if (!canClaim())
                {
                    _stopped = true;
                    return;
                }
                int batchSize = Math.Min(_batchSize, _remaining);
                _remaining -= batchSize;
                _claim = startClaim(batchSize);
            }
        }

    }
}
