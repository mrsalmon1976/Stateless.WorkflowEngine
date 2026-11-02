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

namespace Stateless.WorkflowEngine
{
    public class WorkflowServer : IWorkflowServer
    {
        private readonly object _nodeMonitorLock = new object();
        private WorkflowNodeMonitor _nodeMonitor;

        public WorkflowServer(IWorkflowStore workflowStore) : this(workflowStore, new WorkflowServerOptions())
        {
        }

        public WorkflowServer(IWorkflowStore workflowStore, WorkflowServerOptions options)
        {
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
                            WorkflowNodeMonitor nodeMonitor = new WorkflowNodeMonitor(this.WorkflowStore, this.Options);
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
                    workflow.Fire(workflow.ResumeTrigger);

                    workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
                }
                catch (Exception ex)
                {
                    hasError = true;
                    workflow.CurrentState = initialState;
                    this.WorkflowExceptionHandler.HandleWorkflowException(workflow, ex);

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
                    await workflow.FireAsync(workflow.ResumeTrigger, cancellationToken);

                    workflow.RetryCount = 0;    // success!  make sure the RetryCount is reset
                }
                catch (Exception ex)
                {
                    hasError = true;
                    workflow.CurrentState = initialState;
                    this.WorkflowExceptionHandler.HandleWorkflowException(workflow, ex);

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
        /// DESC, and then by CreationDate.  Workflows are claimed one at a time as execution slots become free, so no more
        /// than <c>maxConcurrent</c> workflows are locked by this server at once, and multiple servers can safely
        /// execute workflows from the same store.
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

            int concurrent = (maxConcurrent ?? count);
            List<Task> tasks = new List<Task>();
            ExceptionDispatchInfo claimError = null;
            WorkflowNodeMonitor nodeMonitor = this.NodeMonitor;
            nodeMonitor.EnsureStarted();

            using (SemaphoreSlim semaphore = new SemaphoreSlim(concurrent))
            {
                while (tasks.Count < count)
                {
                    semaphore.Wait();

                    if (!nodeMonitor.CanClaim)
                    {
                        semaphore.Release();
                        break;
                    }

                    string nodeId = nodeMonitor.NodeId;
                    Workflow workflow;
                    try
                    {
                        workflow = this.WorkflowStore.ClaimNext(nodeId);
                    }
                    catch (Exception ex)
                    {
                        // wait for the workflows already running before raising the error
                        semaphore.Release();
                        claimError = ExceptionDispatchInfo.Capture(ex);
                        break;
                    }

                    if (workflow == null)
                    {
                        semaphore.Release();
                        break;
                    }

                    nodeMonitor.Track(workflow.Id);
                    OnWorkflowClaimed(workflow, nodeId);
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            this.ExecuteWorkflow(workflow, nodeId);
                        }
                        finally
                        {
                            nodeMonitor.Untrack(workflow.Id);
                            semaphore.Release();
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }

            claimError?.Throw();
            return tasks.Count;
        }

        /// <summary>
        /// Claims and executes up to <c>count</c> workflows from the registered store, ordered by Priority DESC, RetryCount
        /// DESC, and then by CreationDate.  Workflows are claimed one at a time as execution slots become free, so no more
        /// than <c>maxConcurrent</c> workflows are locked by this server at once, and multiple servers can safely
        /// execute workflows from the same store.
        /// </summary>
        /// <param name="count">The maximum number of workflows to execute.</param>
        /// <param name="maxConcurrent">The maximum number of workflows to processing parallel - defaults to the value of <c>count</c>.</param>
        /// <returns>The number of workflows that were actually executed.</returns>
        public async Task<int> ExecuteWorkflowsAsync(int count, int? maxConcurrent = null, CancellationToken? cancellationToken = null)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count", "Count parameter must be greater than 0");
            }

            int concurrent = (maxConcurrent ?? count);
            List<Task> tasks = new List<Task>();
            CancellationToken ct = cancellationToken ?? CancellationToken.None;
            ExceptionDispatchInfo claimError = null;
            WorkflowNodeMonitor nodeMonitor = this.NodeMonitor;
            nodeMonitor.EnsureStarted();

            using (SemaphoreSlim semaphore = new SemaphoreSlim(concurrent))
            {
                while (tasks.Count < count)
                {
                    try
                    {
                        await semaphore.WaitAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (ct.IsCancellationRequested || !nodeMonitor.CanClaim)
                    {
                        semaphore.Release();
                        break;
                    }

                    string nodeId = nodeMonitor.NodeId;
                    Workflow workflow;
                    try
                    {
                        workflow = await this.WorkflowStore.ClaimNextAsync(nodeId);
                    }
                    catch (Exception ex)
                    {
                        // wait for the workflows already running before raising the error
                        semaphore.Release();
                        claimError = ExceptionDispatchInfo.Capture(ex);
                        break;
                    }

                    if (workflow == null)
                    {
                        semaphore.Release();
                        break;
                    }

                    // the token is not passed to Task.Run: a claimed workflow must always run (and save), or it would stay
                    // locked until the node's stale locks are released
                    nodeMonitor.Track(workflow.Id);
                    OnWorkflowClaimed(workflow, nodeId);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await this.ExecuteWorkflowAsync(workflow, nodeId, ct);
                        }
                        finally
                        {
                            nodeMonitor.Untrack(workflow.Id);
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            claimError?.Throw();
            return tasks.Count;
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
        /// Stops the server from claiming workflows, waits (up to the node timeout) for workflows it is executing to
        /// finish, and then releases its locks and removes its node so that other servers can pick up its workflows
        /// immediately.  Call this when the application shuts down.
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
            catch
            {
                // the workflow is locked by this node, so it must be executed (and released) whatever the handler does
            }
        }

        private void OnWorkflowLockLost(Workflow workflow, string ownerNodeId)
        {
            var lockLostHandler = this.WorkflowLockLost;
            if (lockLostHandler != null)
            {
                lockLostHandler(this, new WorkflowLockLostEventArgs(workflow, ownerNodeId));
            }
        }

    }
}
