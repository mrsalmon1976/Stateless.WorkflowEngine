using System;

namespace Stateless.WorkflowEngine
{
    /// <summary>
    /// A dependency resolver that creates a scope for each workflow action: the action is resolved from the scope, and
    /// the scope (with any scoped dependencies of the action) is disposed once the action has executed.
    /// </summary>
    public interface IWorkflowEngineScopedDependencyResolver : IWorkflowEngineDependencyResolver
    {
        /// <summary>
        /// Creates the scope that a workflow action is resolved from.
        /// </summary>
        IWorkflowEngineDependencyScope CreateScope();
    }

    /// <summary>
    /// A scope created by an <see cref="IWorkflowEngineScopedDependencyResolver"/> for one workflow action execution.
    /// </summary>
    public interface IWorkflowEngineDependencyScope : IDisposable, IAsyncDisposable
    {
        T GetInstance<T>() where T : class;
    }
}
