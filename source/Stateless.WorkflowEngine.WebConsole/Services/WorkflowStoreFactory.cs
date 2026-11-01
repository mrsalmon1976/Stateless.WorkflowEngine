using MongoDB.Driver;
using Raven.Client.Documents;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.RavenDb;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

namespace Stateless.WorkflowEngine.WebConsole.Services
{
    public interface IWorkflowStoreFactory
    {
        IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel);

        /// <summary>
        /// Removes any cached client for the connection - call this when a connection is edited or deleted.
        /// </summary>
        void Evict(Guid connectionId);
    }

    public class WorkflowStoreFactory : IWorkflowStoreFactory, IDisposable
    {
        public static readonly TimeSpan ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        public const int DefaultMongoDbPort = 27017;

        public const int DefaultRavenDbPort = 8080;

        private readonly IConnectionPasswordProtector _connectionPasswordProtector;

        // MongoClient and DocumentStore instances are designed to be long-lived, so they are cached per connection
        private readonly ConcurrentDictionary<Guid, IDisposable> _clients = new ConcurrentDictionary<Guid, IDisposable>();

        public WorkflowStoreFactory(IConnectionPasswordProtector connectionPasswordProtector)
        {
            _connectionPasswordProtector = connectionPasswordProtector;
        }

        public IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel)
        {
            switch (connectionModel.WorkflowStoreType)
            {
                case WorkflowStoreType.MongoDb:
                    // connections are evicted when edited, so a cached client is always for the current store type
                    MongoClient client = (MongoClient)_clients.GetOrAdd(connectionModel.Id, _ => new MongoClient(GetMongoUrl(connectionModel)));
                    IMongoDatabase db = client.GetDatabase(connectionModel.Database);
                    return new MongoDbWorkflowStore(db, connectionModel.ActiveCollection, connectionModel.CompletedCollection, NullIfEmpty(connectionModel.WorkflowDefinitionCollection));
                case WorkflowStoreType.RavenDb:
                    IDocumentStore documentStore = (IDocumentStore)_clients.GetOrAdd(connectionModel.Id, _ => CreateDocumentStore(connectionModel));
                    return new RavenDbWorkflowStore(documentStore);
                default:
                    throw new NotSupportedException($"Workflow store type {connectionModel.WorkflowStoreType} is not supported");
            }
        }

        public void Evict(Guid connectionId)
        {
            if (_clients.TryRemove(connectionId, out IDisposable? client))
            {
                // closes the client's connections - a request still using it will fail, which only happens when the
                // connection is being edited or deleted at the same time
                client.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (Guid connectionId in _clients.Keys)
            {
                Evict(connectionId);
            }
        }

        public MongoUrl GetMongoUrl(ConnectionModel connectionModel)
        {
            MongoUrlBuilder urlBuilder = new MongoUrlBuilder();
            urlBuilder.Server = new MongoServerAddress(connectionModel.Host, connectionModel.Port ?? DefaultMongoDbPort);
            // fail fast so an unavailable store doesn't hang the dashboard (the driver default is 30 seconds)
            urlBuilder.ServerSelectionTimeout = ServerSelectionTimeout;
            urlBuilder.ConnectTimeout = ServerSelectionTimeout;
            if (!String.IsNullOrWhiteSpace(connectionModel.User))
            {
                urlBuilder.Username = connectionModel.User;
            }
            string? password = _connectionPasswordProtector.Unprotect(connectionModel);
            if (!String.IsNullOrEmpty(password))
            {
                urlBuilder.Password = password;
            }
            if (!String.IsNullOrWhiteSpace(connectionModel.ReplicaSet))
            {
                urlBuilder.ReplicaSetName = connectionModel.ReplicaSet;
            }
            return urlBuilder.ToMongoUrl();
        }

        /// <summary>
        /// Gets the RavenDb server url - secured servers (which require a client certificate) use https.
        /// </summary>
        public string GetRavenDbUrl(ConnectionModel connectionModel)
        {
            string scheme = String.IsNullOrWhiteSpace(connectionModel.CertificatePath) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;
            return new UriBuilder(scheme, connectionModel.Host, connectionModel.Port ?? DefaultRavenDbPort).Uri.GetLeftPart(UriPartial.Authority);
        }

        public IDocumentStore CreateDocumentStore(ConnectionModel connectionModel)
        {
            DocumentStore documentStore = new DocumentStore
            {
                Urls = new[] { GetRavenDbUrl(connectionModel) },
                Database = connectionModel.Database
            };
            // fail fast so an unavailable store doesn't hang the dashboard
            documentStore.Conventions.RequestTimeout = ServerSelectionTimeout;
            if (!String.IsNullOrWhiteSpace(connectionModel.CertificatePath))
            {
                documentStore.Certificate = X509CertificateLoader.LoadPkcs12FromFile(connectionModel.CertificatePath, _connectionPasswordProtector.Unprotect(connectionModel));
            }
            return documentStore.Initialize();
        }

        private static string? NullIfEmpty(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
