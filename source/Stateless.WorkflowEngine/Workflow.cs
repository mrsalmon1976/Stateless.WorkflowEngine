using Microsoft.Extensions.DependencyInjection;
using Stateless.WorkflowEngine.Diagnostics;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleToAttribute("Test.Stateless.WorkflowEngine")]

namespace Stateless.WorkflowEngine
{
    public abstract class Workflow
    {
        private WorkflowEngineInstrumentation _instrumentation;

        public Workflow() : this("Start")
        {
        }

        public Workflow(string initialState)
        {
            // set default values : always initialise with a unique id - this can be set when an existing workflow is loaded from the store
            this.Id = Guid.NewGuid();
            this.CreatedOn = DateTime.UtcNow;

            // default retry intervals to 5 retries of 5, 10, 15, 30, 60 seconds
            this.RetryIntervals = new[] { 5, 10, 15, 30, 60 };

            this.Initialise(initialState);
        }

        /// <summary>
        /// Gets/sets the workflow unique identifier.
        /// </summary>
        public virtual Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the creation date.
        /// </summary>
        /// <value>
        /// The creation date.
        /// </value>
        public virtual DateTime CreatedOn { get; set; }

        /// <summary>
        /// The date/time the workflow completed.
        /// </summary>
        public virtual DateTime? CompletedOn { get; internal set; }

        /// <summary>
        /// Gets/sets the service provider that workflow actions are created from.  This is set by the workflow server
        /// before executing a workflow step.
        /// </summary>
        internal IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// Gets/sets the instrumentation that records the workflow's action metrics and activities.  This is set by the
        /// workflow server before executing a workflow step - if it isn't, the default instrumentation is used.
        /// </summary>
        internal WorkflowEngineInstrumentation Instrumentation
        {
            get => _instrumentation ?? WorkflowEngineInstrumentation.Default;
            set => _instrumentation = value;
        }

        /// <summary>
        /// Marks a workflow as suspended.  This will be set to true when the maximum retry count is exceeded.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is suspended; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsSuspended { get; set; }

        /// <summary>
        /// Gets/sets the last exception info pertaining to the workflow.
        /// </summary>
        public virtual string LastException { get; set; }

        /// <summary>
        /// Gets the name of the workflow (class name).  Provides a setter for serialisation into document databases, but 
        /// programmatically setting this value will have no impact.
        /// </summary>
        public virtual string Name 
        { 
            get 
            {
                return this.GetType().Name;
            } 
            set
            {
                // does nothing
            }
        }

        /// <summary>
        /// Gets/sets the time the workflow can be resumed.  Set to DateTime.Min to ensure workflows are picked up - nullable 
        /// properties are not used here as MongoDb's Query does not support it.
        /// </summary>
        public virtual DateTime ResumeOn { get; set; }

        /// <summary>
        /// Gets/sets the priority of the workflow.  Defaults to 0, but can be set at runtime.  Higher priority 
        /// workflows (where 2 is higher than 1) will be executed first.
        /// </summary>
        public virtual int Priority { get; set; }

        /// <summary>
        /// Gets or sets the trigger to be fire when the workflow resumes.
        /// </summary>
        /// <value>
        /// The trigger.
        /// </value>
        public virtual string ResumeTrigger { get; set; }

        /// <summary>
        /// Gets or sets the retry count for the workflow at the current step.  This gets reset to 0 when a step successfully completes, 
        /// and will increment each time a step fails.
        /// </summary>
        /// <value>
        /// The retry count.
        /// </value>
        public virtual int RetryCount { get; set; }

        /// <summary>
        /// Gets/sets the retry intervals, in seconds, of the workflow.
        /// </summary>
        public virtual int[] RetryIntervals { get; set; }

        /// <summary>
        /// Gets the qualified name of the workflow (full qualified class name).  Provides a setter for serialisation into document databases, but 
        /// programmatically setting this value will have no impact.
        /// </summary>
        public virtual string QualifiedName
        {
            get
            {
                return this.GetType().FullName;
            }
            set
            {
                // does nothing
            }
        }

        /// <summary>
        /// Gets/sets the current state of the workflow.
        /// </summary>
        public virtual string CurrentState { get; set; }

        /// <summary>
        /// Gets/sets whether the workflow is complete.
        /// </summary>
        public virtual bool IsComplete { get; set; }

        /// <summary>
        /// Gets whether the worker is a single-instance type workflow worker.
        /// </summary>
        public virtual bool IsSingleInstance { get; set; }

        /// <summary>
        /// Gets/sets the id of the workflow server node that has claimed the workflow for execution, or null if the
        /// workflow is available to be claimed.  This is managed by the workflow server and should not be set manually.
        /// </summary>
        public virtual string LockedBy { get; set; }

        /// <summary>
        /// Gets/sets the UTC date/time the workflow was claimed by the node in <c>LockedBy</c>, or null if it is not
        /// locked.  This is managed by the workflow server and should not be set manually.
        /// </summary>
        public virtual DateTime? LockedOn { get; set; }

        /// <summary>
        /// Creates a new WorkflowAction instance.  The action is resolved from <paramref name="serviceProvider"/> (or
        /// created with its constructor dependencies from it if it isn't registered); if the workflow server has no
        /// service provider, it is created with its parameterless constructor.  This can be overridden if you'd like
        /// to create the actions yourself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceProvider">The scope the action executes in, or null if the workflow server has no
        /// service provider.</param>
        protected virtual T CreateWorkflowActionInstance<T>(IServiceProvider serviceProvider) where T : class, IWorkflowAction
        {
            return CreateInstance<T>(serviceProvider);
        }

        /// <summary>
        /// Creates a new WorkflowActionAsync instance.  The action is resolved from <paramref name="serviceProvider"/>
        /// (or created with its constructor dependencies from it if it isn't registered); if the workflow server has
        /// no service provider, it is created with its parameterless constructor.  This can be overridden if you'd
        /// like to create the actions yourself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceProvider">The scope the action executes in, or null if the workflow server has no
        /// service provider.</param>
        protected virtual T CreateWorkflowActionAsyncInstance<T>(IServiceProvider serviceProvider) where T : class, IWorkflowActionAsync
        {
            return CreateInstance<T>(serviceProvider);
        }

        private static T CreateInstance<T>(IServiceProvider serviceProvider) where T : class
        {
            return serviceProvider == null
                ? Activator.CreateInstance<T>()
                : ActivatorUtilities.GetServiceOrCreateInstance<T>(serviceProvider);
        }

        /// <summary>
        /// Gets a graph representation of the workflow in the DOT graph language.
        /// </summary>
        /// <returns></returns>
        public abstract string GetGraph();

        /// <summary>
        /// Initialises and configures the workflow.
        /// </summary>
        /// <param name="initialState"></param>
        public abstract void Initialise(string initialState);

        /// <summary>
        /// Fires a trigger - this needs to be a trigger configured for the current state.
        /// </summary>
        /// <param name="triggerName"></param>
        public abstract void Fire(string triggerName);

        /// <summary>
        /// Fires a trigger asynchronously - this needs to be a trigger configured for the current state.
        /// </summary>
        /// <param name="triggerName"></param>
        public abstract Task FireAsync(string triggerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates (with <see cref="CreateWorkflowActionInstance{T}"/>) and executes a workflow action.  If the
        /// workflow server has a service provider, the action is created in a new scope, which is disposed once the
        /// action has executed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected virtual void ExecuteWorkflowAction<T>() where T : class, IWorkflowAction
        {
            if (this.ServiceProvider == null)
            {
                this.RunWorkflowAction(this.CreateWorkflowActionInstance<T>(null));
                return;
            }

            AsyncServiceScope scope = this.ServiceProvider.CreateAsyncScope();
            try
            {
                this.RunWorkflowAction(this.CreateWorkflowActionInstance<T>(scope.ServiceProvider));
            }
            finally
            {
                // disposed asynchronously so services that are only IAsyncDisposable don't throw
                scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Creates (with <see cref="CreateWorkflowActionAsyncInstance{T}"/>) and executes a workflow action.  If the
        /// workflow server has a service provider, the action is created in a new scope, which is disposed once the
        /// action has executed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected async virtual Task ExecuteWorkflowActionAsync<T>(CancellationToken cancellationToken = default) where T : class, IWorkflowActionAsync
        {
            if (this.ServiceProvider == null)
            {
                await this.RunWorkflowActionAsync(this.CreateWorkflowActionAsyncInstance<T>(null), cancellationToken);
                return;
            }

            await using (AsyncServiceScope scope = this.ServiceProvider.CreateAsyncScope())
            {
                await this.RunWorkflowActionAsync(this.CreateWorkflowActionAsyncInstance<T>(scope.ServiceProvider), cancellationToken);
            }
        }

        private void RunWorkflowAction(IWorkflowAction workflowAction)
        {
            WorkflowEngineInstrumentation instrumentation = this.Instrumentation;
            using Activity activity = instrumentation.StartAction(this, workflowAction);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                this.OnActionExecuting(workflowAction);
                workflowAction.Execute(this);
                this.OnActionExecuted(workflowAction);
            }
            catch (Exception ex)
            {
                instrumentation.ActionExecuted(activity, this, workflowAction, startTimestamp, ex);
                throw;
            }
            instrumentation.ActionExecuted(activity, this, workflowAction, startTimestamp, null);
        }

        private async Task RunWorkflowActionAsync(IWorkflowActionAsync workflowAction, CancellationToken cancellationToken)
        {
            WorkflowEngineInstrumentation instrumentation = this.Instrumentation;
            using Activity activity = instrumentation.StartAction(this, workflowAction);
            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await this.OnActionExecutingAsync(workflowAction, cancellationToken);
                await workflowAction.ExecuteAsync(this, cancellationToken);
                await this.OnActionExecutedAsync(workflowAction, cancellationToken);
            }
            catch (Exception ex)
            {
                instrumentation.ActionExecuted(activity, this, workflowAction, startTimestamp, ex);
                throw;
            }
            instrumentation.ActionExecuted(activity, this, workflowAction, startTimestamp, null);
        }

        /// <summary>
        /// Fires before a workflow action is executed by the engine.  Useful for inserting logging to your workflow.
        /// Only fires on ExecuteWorkflowAction method call.
        /// </summary>
        /// <param name="action"></param>
        public virtual void OnActionExecuting(IWorkflowAction action)
        {

        }

        /// <summary>
        /// Fires before a workflow action is executed by the engine.  Useful for inserting logging to your workflow.
        /// Only fires on ExecuteWorkflowActionAsync method call.
        /// </summary>
        /// <param name="action"></param>
        public virtual Task OnActionExecutingAsync(IWorkflowActionAsync action, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Fires after a workflow action is executed by the engine.  Useful for inserting logging to your workflow.
        /// Only fires on ExecuteWorkflowAction method.
        /// </summary>
        /// <param name="action"></param>
        public virtual void OnActionExecuted(IWorkflowAction action)
        {

        }

        /// <summary>
        /// Fires after a workflow action is executed by the engine.  Useful for inserting logging to your workflow.
        /// Only fires on ExecuteWorkflowActionAsync method.
        /// </summary>
        /// <param name="action"></param>
        public virtual Task OnActionExecutedAsync(IWorkflowActionAsync action, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fires when the workflow is suspended.  This can be overridden to handle any workflow-specific 
        /// suspension activities.
        /// </summary>
        public virtual void OnSuspend()
        {
        }

        /// <summary>
        /// Fires when the workflow completes.  This can be overridden to handle any workflow-specific 
        /// completion activities.
        /// </summary>
        public virtual void OnComplete()
        {
        }

        /// <summary>
        /// Fires when an error happens within the workflow.  This can be overriden to add logging, or any other 
        /// exception management.
        /// </summary>
        public virtual void OnError(Exception ex)
        {
        }

    }
}
