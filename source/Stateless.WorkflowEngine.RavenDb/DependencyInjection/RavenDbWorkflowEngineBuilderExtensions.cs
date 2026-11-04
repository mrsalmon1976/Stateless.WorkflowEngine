using Raven.Client.Documents;
using Stateless.WorkflowEngine.RavenDb;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RavenDbWorkflowEngineBuilderExtensions
    {
        /// <summary>
        /// Stores workflows in a RavenDb database.  The <see cref="DocumentStore"/> is created and initialised for the
        /// workflow store, and disposed with the service provider.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="urls">The urls of the RavenDb server(s).</param>
        /// <param name="database">The database holding the workflows.</param>
        /// <param name="certificate">The client certificate for a secured server, or null.</param>
        public static IWorkflowEngineBuilder WithRavenDbStore(this IWorkflowEngineBuilder builder, string[] urls, string database, X509Certificate2 certificate = null)
        {
            if (urls == null || urls.Length == 0)
            {
                throw new ArgumentException("At least one RavenDb url must be supplied", nameof(urls));
            }
            if (String.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentException("A RavenDb database name must be supplied", nameof(database));
            }

            // registered with a factory (not as an instance) so the service provider disposes it
            RavenDbStoreDocumentStore storeDocumentStore = new RavenDbStoreDocumentStore(urls, database, certificate);
            builder.Services.AddSingleton(sp => storeDocumentStore);
            return builder.WithRavenDbStore(sp =>
            {
                // resolved so the service provider tracks the holder for disposal
                sp.GetRequiredService<RavenDbStoreDocumentStore>();
                return storeDocumentStore.DocumentStore;
            });
        }

        /// <summary>
        /// Stores workflows in the RavenDb document store returned by the factory, which must be initialised - use this
        /// to share a document store that is registered with the service provider.
        /// </summary>
        public static IWorkflowEngineBuilder WithRavenDbStore(this IWorkflowEngineBuilder builder, Func<IServiceProvider, IDocumentStore> documentStoreFactory)
        {
            return builder.WithStore(sp => new RavenDbWorkflowStore(documentStoreFactory(sp)));
        }

        /// <summary>
        /// Owns the document store created by <c>WithRavenDbStore(urls, ...)</c>, so it is disposed with the service
        /// provider.  The document store is only created once the workflow store is first resolved.
        /// </summary>
        internal sealed class RavenDbStoreDocumentStore : IDisposable
        {
            private readonly Lazy<IDocumentStore> _documentStore;

            public RavenDbStoreDocumentStore(string[] urls, string database, X509Certificate2 certificate)
            {
                _documentStore = new Lazy<IDocumentStore>(() => new DocumentStore
                {
                    Urls = urls,
                    Database = database,
                    Certificate = certificate
                }.Initialize());
            }

            public IDocumentStore DocumentStore => _documentStore.Value;

            public void Dispose()
            {
                if (_documentStore.IsValueCreated)
                {
                    _documentStore.Value.Dispose();
                }
            }
        }
    }
}
