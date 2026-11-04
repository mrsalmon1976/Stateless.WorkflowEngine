using Microsoft.Extensions.DependencyInjection.Extensions;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.DependencyInjection;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class WorkflowEngineBuilderExtensions
    {
        /// <summary>
        /// Registers a workflow type with the server when it is created (see <c>IWorkflowServer.RegisterWorkflowType</c>).
        /// Add every workflow type the server executes.
        /// </summary>
        public static IWorkflowEngineBuilder AddWorkflowType<T>(this IWorkflowEngineBuilder builder) where T : Workflow
        {
            if (!builder.Services.Any(x => x.ServiceType == typeof(WorkflowTypeRegistration) && (x.ImplementationInstance as WorkflowTypeRegistration)?.WorkflowType == typeof(T)))
            {
                builder.Services.AddSingleton(new WorkflowTypeRegistration(typeof(T), server => server.RegisterWorkflowType<T>()));
            }
            return builder;
        }

        /// <summary>
        /// Registers every workflow action (<see cref="IWorkflowAction"/> and <see cref="IWorkflowActionAsync"/>) in the
        /// assembly of <typeparamref name="T"/> as transient, unless it is already registered.  Actions don't have to be
        /// registered to be created, but registered actions are checked by the service provider's <c>ValidateOnBuild</c>,
        /// so missing dependencies are found at startup rather than when the action runs.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="filter">Returns true for the action types to register, e.g. those in one namespace - or null to
        /// register them all.</param>
        public static IWorkflowEngineBuilder AddWorkflowActionsFromAssemblyOf<T>(this IWorkflowEngineBuilder builder, Func<Type, bool> filter = null)
        {
            return builder.AddWorkflowActionsFromAssembly(typeof(T).Assembly, filter);
        }

        /// <summary>
        /// Registers every workflow action (<see cref="IWorkflowAction"/> and <see cref="IWorkflowActionAsync"/>) in the
        /// assembly as transient, unless it is already registered.  Actions don't have to be registered to be created,
        /// but registered actions are checked by the service provider's <c>ValidateOnBuild</c>, so missing dependencies
        /// are found at startup rather than when the action runs.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="assembly"></param>
        /// <param name="filter">Returns true for the action types to register, e.g. those in one namespace - or null to
        /// register them all.</param>
        public static IWorkflowEngineBuilder AddWorkflowActionsFromAssembly(this IWorkflowEngineBuilder builder, Assembly assembly, Func<Type, bool> filter = null)
        {
            foreach (Type actionType in GetLoadableTypes(assembly).Where(x => IsWorkflowAction(x) && (filter == null || filter(x))))
            {
                builder.Services.TryAddTransient(actionType);
            }
            return builder;
        }

        /// <summary>
        /// Stores workflows in memory (see <see cref="MemoryWorkflowStore"/>) - workflows are lost when the application
        /// stops.
        /// </summary>
        public static IWorkflowEngineBuilder WithMemoryStore(this IWorkflowEngineBuilder builder)
        {
            return builder.WithStore(sp => new MemoryWorkflowStore());
        }

        /// <summary>
        /// Uses a custom workflow store, created by the service provider.
        /// </summary>
        public static IWorkflowEngineBuilder WithStore<TStore>(this IWorkflowEngineBuilder builder) where TStore : class, IWorkflowStore
        {
            return builder.WithStore(sp => ActivatorUtilities.GetServiceOrCreateInstance<TStore>(sp));
        }

        /// <summary>
        /// Uses the workflow store created by the factory.  The store is a singleton, shared by the server and client,
        /// and replaces any store added before.
        /// </summary>
        public static IWorkflowEngineBuilder WithStore(this IWorkflowEngineBuilder builder, Func<IServiceProvider, IWorkflowStore> storeFactory)
        {
            builder.Services.RemoveAll<IWorkflowStore>();
            builder.Services.AddSingleton(storeFactory);
            return builder;
        }

        private static bool IsWorkflowAction(Type type)
        {
            return type.IsClass
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && (typeof(IWorkflowAction).IsAssignableFrom(type) || typeof(IWorkflowActionAsync).IsAssignableFrom(type));
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null).ToArray();
            }
        }
    }
}
