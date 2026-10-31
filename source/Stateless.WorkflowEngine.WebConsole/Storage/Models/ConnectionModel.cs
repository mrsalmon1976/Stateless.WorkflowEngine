namespace Stateless.WorkflowEngine.WebConsole.Storage.Models
{
    public class ConnectionModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

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
    }
}
