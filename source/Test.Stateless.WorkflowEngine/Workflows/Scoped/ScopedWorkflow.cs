using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stateless.WorkflowEngine;

namespace Test.Stateless.WorkflowEngine.Workflows.Scoped
{
    /// <summary>
    /// Workflow that runs actions directly, to test how they are created and scoped.
    /// </summary>
    public class ScopedWorkflow : StateWorkflow<ScopedWorkflow.State, ScopedWorkflow.Trigger>
    {
        public enum State { Start }
        public enum Trigger { Start }

        public ScopedWorkflow() : base(State.Start.ToString()) { }

        public override void Initialise(string initialState)
        {
            base.Initialise(initialState);
            this.Configure(State.Start);
        }

        /// <summary>
        /// The dependencies the actions were created with, in execution order.
        /// </summary>
        public List<ScopedDependency> Dependencies { get; } = new List<ScopedDependency>();

        public void RunAction<T>() where T : class, IWorkflowAction
            => ExecuteWorkflowAction<T>();

        public Task RunActionAsync<T>(CancellationToken cancellationToken = default) where T : class, IWorkflowActionAsync
            => ExecuteWorkflowActionAsync<T>(cancellationToken);
    }

    /// <summary>
    /// Workflow that creates its actions itself, recording the service providers it was given.
    /// </summary>
    public class ActionCreatingScopedWorkflow : ScopedWorkflow
    {
        public List<IServiceProvider> ServiceProviders { get; } = new List<IServiceProvider>();

        protected override T CreateWorkflowActionInstance<T>(IServiceProvider serviceProvider)
        {
            this.ServiceProviders.Add(serviceProvider);
            return (T)(object)new ScopedAction(serviceProvider?.GetService(typeof(ScopedDependency)) as ScopedDependency ?? new ScopedDependency(new AsyncOnlyDependency()));
        }

        protected override T CreateWorkflowActionAsyncInstance<T>(IServiceProvider serviceProvider)
        {
            this.ServiceProviders.Add(serviceProvider);
            return (T)(object)new ScopedAsyncAction(serviceProvider?.GetService(typeof(ScopedDependency)) as ScopedDependency ?? new ScopedDependency(new AsyncOnlyDependency()));
        }
    }

    /// <summary>
    /// A dependency registered as scoped, which records when it is disposed.
    /// </summary>
    public class ScopedDependency : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public AsyncOnlyDependency AsyncOnlyDependency { get; }

        public ScopedDependency(AsyncOnlyDependency asyncOnlyDependency)
        {
            this.AsyncOnlyDependency = asyncOnlyDependency;
        }

        public void Dispose()
        {
            this.IsDisposed = true;
        }
    }

    /// <summary>
    /// A scoped dependency that can only be disposed asynchronously - a synchronous dispose of its scope would throw.
    /// </summary>
    public class AsyncOnlyDependency : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            this.IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    public class ScopedAction : IWorkflowAction
    {
        private readonly ScopedDependency _dependency;

        public ScopedAction(ScopedDependency dependency)
        {
            _dependency = dependency;
        }

        public void Execute(Workflow workflow)
        {
            ((ScopedWorkflow)workflow).Dependencies.Add(_dependency);
        }
    }

    public class ScopedAsyncAction : IWorkflowActionAsync
    {
        private readonly ScopedDependency _dependency;

        public ScopedAsyncAction(ScopedDependency dependency)
        {
            _dependency = dependency;
        }

        public Task ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            ((ScopedWorkflow)workflow).Dependencies.Add(_dependency);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Not registered by an assembly scan, as it can't be created.
    /// </summary>
    public abstract class AbstractScopedAction : IWorkflowAction
    {
        public abstract void Execute(Workflow workflow);
    }

    /// <summary>
    /// Not registered by an assembly scan, as it is an open generic type.
    /// </summary>
    public class GenericScopedAction<T> : IWorkflowAction
    {
        public void Execute(Workflow workflow)
        {
        }
    }
}
