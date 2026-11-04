using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MongoDbWorkflowEngineBuilderExtensions
    {
        /// <summary>
        /// Stores workflows in a MongoDb database.  The <see cref="MongoClient"/> is created for the workflow store, and
        /// disposed with the service provider.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="connectionString">The MongoDb connection string.</param>
        /// <param name="databaseName">The database holding the workflow collections.</param>
        /// <param name="configure">Configures the store once it is created, e.g. to change the collection names.</param>
        public static IWorkflowEngineBuilder WithMongoDbStore(this IWorkflowEngineBuilder builder, string connectionString, string databaseName, Action<MongoDbWorkflowStore> configure = null)
        {
            if (String.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("A MongoDb connection string must be supplied", nameof(connectionString));
            }
            if (String.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("A MongoDb database name must be supplied", nameof(databaseName));
            }

            // registered with a factory (not as an instance) so the service provider disposes it
            MongoDbStoreClient storeClient = new MongoDbStoreClient(connectionString);
            builder.Services.AddSingleton(sp => storeClient);
            return builder.WithMongoDbStore(sp =>
            {
                // resolved so the service provider tracks the holder for disposal
                sp.GetRequiredService<MongoDbStoreClient>();
                return storeClient.Client.GetDatabase(databaseName);
            }, configure);
        }

        /// <summary>
        /// Stores workflows in the MongoDb database returned by the factory - use this to share a client that is
        /// registered with the service provider.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="databaseFactory">Gets the database holding the workflow collections.</param>
        /// <param name="configure">Configures the store once it is created, e.g. to change the collection names.</param>
        public static IWorkflowEngineBuilder WithMongoDbStore(this IWorkflowEngineBuilder builder, Func<IServiceProvider, IMongoDatabase> databaseFactory, Action<MongoDbWorkflowStore> configure = null)
        {
            return builder.WithStore(sp =>
            {
                MongoDbWorkflowStore store = new MongoDbWorkflowStore(databaseFactory(sp));
                configure?.Invoke(store);
                return store;
            });
        }

        /// <summary>
        /// Owns the client created by <c>WithMongoDbStore(connectionString, ...)</c>, so it is disposed with the service
        /// provider.  The client is only created once the store is first resolved.
        /// </summary>
        internal sealed class MongoDbStoreClient : IDisposable
        {
            private readonly Lazy<MongoClient> _client;

            public MongoDbStoreClient(string connectionString)
            {
                _client = new Lazy<MongoClient>(() => new MongoClient(connectionString));
            }

            public MongoClient Client => _client.Value;

            public void Dispose()
            {
                if (_client.IsValueCreated)
                {
                    _client.Value.Dispose();
                }
            }
        }
    }
}
