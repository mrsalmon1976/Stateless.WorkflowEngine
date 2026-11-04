using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Stateless.WorkflowEngine.Diagnostics;

namespace Example.Shared
{
    /// <summary>
    /// Builds the service provider for an example: the workflow engine (server, client and store), logging, and anything
    /// the example's workflow actions depend on.
    /// </summary>
    internal static class ExampleServices
    {
        /// <param name="storeType"><see cref="Constants.StoreTypeMongoDb"/> or <see cref="Constants.StoreTypeRavenDb"/>.</param>
        /// <param name="dbName">The MongoDb or RavenDb database holding the workflows.</param>
        /// <param name="configureEngine">Adds the example's workflow types and actions, and sets server options.</param>
        /// <param name="engineLogLevel">The minimum level logged by the workflow engine - other categories log warnings and errors.</param>
        /// <param name="configureServices">Registers the services the example's workflow actions depend on.</param>
        /// <param name="loggerProvider">Where logs are written - defaults to the console.</param>
        public static ServiceProvider Create(string storeType, string dbName, Action<IWorkflowEngineBuilder>? configureEngine = null, LogLevel engineLogLevel = LogLevel.Information, Action<IServiceCollection>? configureServices = null, ILoggerProvider? loggerProvider = null)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging(logging =>
            {
                if (loggerProvider == null)
                {
                    logging.AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                    });
                }
                else
                {
                    logging.AddProvider(loggerProvider);
                }
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddFilter("Stateless.WorkflowEngine", engineLogLevel);
            });

            IWorkflowEngineBuilder engine = services.AddStatelessWorkflowEngine();
            if (storeType == Constants.StoreTypeMongoDb)
            {
                engine.WithMongoDbStore(AppSettings.MongoDbConnectionString, dbName);
            }
            else if (storeType == Constants.StoreTypeRavenDb)
            {
                engine.WithRavenDbStore(new[] { AppSettings.RavenDbUrl }, dbName);
            }
            else
            {
                throw new InvalidOperationException($"Store type '{storeType}' not supported");
            }
            configureEngine?.Invoke(engine);
            configureServices?.Invoke(services);

            // the engine's logs, metrics and traces are exported to the Aspire dashboard (if it isn't running, export fails
            // quietly) - logs include the formatted message, as the body is otherwise the message template
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("Stateless.WorkflowEngine.Example"))
                .WithLogging(configureBuilder: null, configureOptions: options => options.IncludeFormattedMessage = true)
                .WithMetrics(metrics => metrics.AddMeter(WorkflowEngineDiagnostics.Name))
                .WithTracing(tracing => tracing.AddSource(WorkflowEngineDiagnostics.Name))
                .UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(AppSettings.OtlpEndpoint));

            // validation checks that every registered workflow action can be created, and that scoped services are
            // only used by actions (each action runs in its own scope)
            ServiceProvider serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            // without a host, the providers only start listening once resolved - they flush when the service provider is disposed
            serviceProvider.GetRequiredService<MeterProvider>();
            serviceProvider.GetRequiredService<TracerProvider>();
            return serviceProvider;
        }

        /// <summary>
        /// Filter for <c>AddWorkflowActionsFromAssemblyOf</c>: true for types in the namespace of
        /// <typeparamref name="T"/> or below it (e.g. a workflow's Actions namespace).  The examples share one assembly, so
        /// each example only registers its own workflow's actions.
        /// </summary>
        public static bool InNamespaceOf<T>(Type type)
        {
            string ns = typeof(T).Namespace!;
            return type.Namespace == ns || (type.Namespace?.StartsWith(ns + ".") ?? false);
        }

        /// <summary>
        /// Tells the user which database the example is about to use.
        /// </summary>
        public static void WriteStoreInfo(string storeType, string dbName)
        {
            if (storeType == Constants.StoreTypeMongoDb)
            {
                Console.WriteLine($"Using MongoDb - please ensure an unauthenticated version of MongoDb is running " + Environment.NewLine +
                    $"on '{AppSettings.MongoDbConnectionString}' (run start.ps1 to start one in Docker); database '{dbName}' will be created.");
            }
            else if (storeType == Constants.StoreTypeRavenDb)
            {
                Console.WriteLine($"Using RavenDb - please ensure RavenDb is running on '{AppSettings.RavenDbUrl}' (run start.ps1 to start one in Docker); " + Environment.NewLine +
                    $"database '{dbName}' will be created.");
            }
        }
    }
}
