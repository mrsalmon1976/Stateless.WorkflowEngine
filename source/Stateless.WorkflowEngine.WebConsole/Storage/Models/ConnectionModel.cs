namespace Stateless.WorkflowEngine.WebConsole.Storage.Models
{
    public class ConnectionModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets/sets the dashboard the connection appears on.  Null for connections saved by versions prior
        /// to dashboards - these are assigned to the default dashboard on load.
        /// </summary>
        public Guid? DashboardId { get; set; }

        public WorkflowStoreType WorkflowStoreType { get; set; }

        public string? Host { get; set; }

        public string? Database { get; set; }

        public string? User { get; set; }

        /// <summary>
        /// Gets/sets the encrypted password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets/sets the key used to encrypt the password (Base-64-encoded byte array).  Only populated for
        /// connections saved by versions prior to 6 - these are migrated to data protection on load.
        /// </summary>
        public string? Key { get; set; }

        public int? Port { get; set; }

        public string? ReplicaSet { get; set; }

        public string? ActiveCollection { get; set; }

        public string? CompletedCollection { get; set; }

        public string? WorkflowDefinitionCollection { get; set; }

        /// <summary>
        /// Returns true if the other connection points at the same workflow store (store type, host, port,
        /// database and collections), regardless of the dashboard or credentials.
        /// </summary>
        public bool IsSameStoreAs(ConnectionModel other)
        {
            return WorkflowStoreType == other.WorkflowStoreType
                && String.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
                && Port == other.Port
                && String.Equals(Database, other.Database, StringComparison.Ordinal)
                && String.Equals(ActiveCollection, other.ActiveCollection, StringComparison.Ordinal)
                && String.Equals(CompletedCollection, other.CompletedCollection, StringComparison.Ordinal);
        }
    }
}
