using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.DependencyInjection;
using Stateless.WorkflowEngine.Diagnostics;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Diagnostics.Metrics;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class WorkflowEngineServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the workflow server (<see cref="IWorkflowServer"/>) and client (<see cref="IWorkflowClient"/>) as
        /// singletons.  A store must also be added, with <c>WithMongoDbStore</c>, <c>WithRavenDbStore</c>,
        /// <c>WithMemoryStore</c> or <c>WithStore</c>.  The server logs through the registered <see cref="ILoggerFactory"/>,
        /// and creates workflow actions from the service provider, each in its own scope.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure">Configures the server options - they can also be configured (e.g. bound to
        /// configuration) with <c>services.Configure&lt;WorkflowServerOptions&gt;()</c>.</param>
        public static IWorkflowEngineBuilder AddStatelessWorkflowEngine(this IServiceCollection services, Action<WorkflowServerOptions> configure = null)
        {
            OptionsBuilder<WorkflowServerOptions> options = services.AddOptions<WorkflowServerOptions>();
            if (configure != null)
            {
                options.Configure(configure);
            }

            // metrics are created from the application's meter factory when there is one (hosts add it), so the
            // container owns the meter
            services.TryAddSingleton(sp =>
            {
                IMeterFactory meterFactory = sp.GetService<IMeterFactory>();
                return meterFactory == null ? WorkflowEngineInstrumentation.Default : new WorkflowEngineInstrumentation(meterFactory);
            });

            services.TryAddSingleton<IWorkflowServer>(sp =>
            {
                WorkflowServer workflowServer = new WorkflowServer(GetWorkflowStore(sp), sp.GetRequiredService<IOptions<WorkflowServerOptions>>().Value, sp.GetService<ILoggerFactory>(), sp.GetRequiredService<WorkflowEngineInstrumentation>());
                workflowServer.ServiceProvider = sp;
                foreach (WorkflowTypeRegistration registration in sp.GetServices<WorkflowTypeRegistration>())
                {
                    registration.Register(workflowServer);
                }
                return workflowServer;
            });

            services.TryAddSingleton<IWorkflowClient>(sp => new WorkflowClient(GetWorkflowStore(sp), sp.GetRequiredService<WorkflowEngineInstrumentation>()));

            return new WorkflowEngineBuilder(services);
        }

        private static IWorkflowStore GetWorkflowStore(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IWorkflowStore>()
                ?? throw new WorkflowException("No workflow store registered - call WithMongoDbStore, WithRavenDbStore, WithMemoryStore or WithStore after AddStatelessWorkflowEngine");
        }
    }
}
