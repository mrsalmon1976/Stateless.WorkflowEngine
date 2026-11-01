using MongoDB.Driver;
using Stateless.WorkflowEngine.MongoDb;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Security;
using System.Collections.Concurrent;

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

        private readonly IConnectionPasswordProtector _connectionPasswordProtector;
        private readonly ConcurrentDictionary<Guid, MongoClient> _clients = new ConcurrentDictionary<Guid, MongoClient>();

        public WorkflowStoreFactory(IConnectionPasswordProtector connectionPasswordProtector)
        {
            _connectionPasswordProtector = connectionPasswordProtector;
        }

        public IWorkflowStore GetWorkflowStore(ConnectionModel connectionModel)
        {
            if (connectionModel.WorkflowStoreType != WorkflowStoreType.MongoDb)
            {
                throw new NotSupportedException($"Workflow store type {connectionModel.WorkflowStoreType} is not supported");
            }

            // MongoClient instances are designed to be long-lived, so cache them per connection
            MongoClient client = _clients.GetOrAdd(connectionModel.Id, _ => new MongoClient(GetMongoUrl(connectionModel)));
            IMongoDatabase db = client.GetDatabase(connectionModel.Database);
            return new MongoDbWorkflowStore(db, connectionModel.ActiveCollection, connectionModel.CompletedCollection, NullIfEmpty(connectionModel.WorkflowDefinitionCollection));
        }

        public void Evict(Guid connectionId)
        {
            if (_clients.TryRemove(connectionId, out MongoClient? client))
            {
                // closes the client's connection pool - a request still using it will fail, which only happens when the
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
            urlBuilder.Server = new MongoServerAddress(connectionModel.Host, connectionModel.Port ?? 27017);
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

        private static string? NullIfEmpty(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
