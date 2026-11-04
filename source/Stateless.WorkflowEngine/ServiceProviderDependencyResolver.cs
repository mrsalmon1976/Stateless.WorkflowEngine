using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine
{
    /// <summary>
    /// Creates workflow actions from a <c>Microsoft.Extensions.DependencyInjection</c> service provider.  Each action is
    /// resolved from its own scope, so it can depend on scoped services (e.g. an Entity Framework <c>DbContext</c>),
    /// which are disposed once the action has executed.  Actions don't need to be registered: an action that isn't
    /// registered is created with its constructor dependencies resolved from the scope.
    /// </summary>
    public class ServiceProviderDependencyResolver : IWorkflowEngineScopedDependencyResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderDependencyResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IWorkflowEngineDependencyScope CreateScope()
        {
            return new ServiceProviderDependencyScope(_serviceProvider.CreateAsyncScope());
        }

        /// <summary>
        /// Gets an instance from the root service provider (not from a scope).
        /// </summary>
        public T GetInstance<T>() where T : class
        {
            return ActivatorUtilities.GetServiceOrCreateInstance<T>(_serviceProvider);
        }

        private sealed class ServiceProviderDependencyScope : IWorkflowEngineDependencyScope
        {
            private readonly AsyncServiceScope _scope;

            public ServiceProviderDependencyScope(AsyncServiceScope scope)
            {
                _scope = scope;
            }

            public T GetInstance<T>() where T : class
            {
                return ActivatorUtilities.GetServiceOrCreateInstance<T>(_scope.ServiceProvider);
            }

            public void Dispose()
            {
                // disposed asynchronously so services that are only IAsyncDisposable don't throw when a synchronous
                // action's scope is disposed
                _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            public ValueTask DisposeAsync()
            {
                return _scope.DisposeAsync();
            }
        }
    }
}
