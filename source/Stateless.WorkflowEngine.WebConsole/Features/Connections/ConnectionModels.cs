using Stateless.WorkflowEngine.WebConsole.Storage.Models;

namespace Stateless.WorkflowEngine.WebConsole.Features.Connections
{
    public class ConnectionRequest
    {
        /// <summary>
        /// Gets/sets the id of an existing connection - only used when testing an edited connection, so the
        /// stored password can be used if a new one is not supplied.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Gets/sets the dashboard to add a new connection to - the default dashboard is used if not supplied.
        /// Ignored when updating, as connections are copied rather than moved between dashboards.
        /// </summary>
        public Guid? DashboardId { get; set; }

        public WorkflowStoreType WorkflowStoreType { get; set; }

        public string? Host { get; set; }

        public int? Port { get; set; }

        public string? Database { get; set; }

        public string? User { get; set; }

        public string? CertificatePath { get; set; }

        public string? Password { get; set; }

        public string? PasswordConfirm { get; set; }

        public string? ReplicaSet { get; set; }

        public string? ActiveCollection { get; set; }

        public string? CompletedCollection { get; set; }

        public string? WorkflowDefinitionCollection { get; set; }
    }

    public class ConnectionCopyRequest
    {
        public Guid DashboardId { get; set; }
    }

    public class ConnectionResponse
    {
        public Guid Id { get; set; }

        public Guid? DashboardId { get; set; }

        public WorkflowStoreType WorkflowStoreType { get; set; }

        public string? Host { get; set; }

        public int? Port { get; set; }

        public string? Database { get; set; }

        public string? User { get; set; }

        public string? CertificatePath { get; set; }

        public bool HasPassword { get; set; }

        public string? ReplicaSet { get; set; }

        public string? ActiveCollection { get; set; }

        public string? CompletedCollection { get; set; }

        public string? WorkflowDefinitionCollection { get; set; }

        public static ConnectionResponse FromModel(ConnectionModel model)
        {
            return new ConnectionResponse
            {
                Id = model.Id,
                DashboardId = model.DashboardId,
                WorkflowStoreType = model.WorkflowStoreType,
                Host = model.Host,
                Port = model.Port,
                Database = model.Database,
                User = model.User,
                CertificatePath = model.CertificatePath,
                HasPassword = !String.IsNullOrEmpty(model.Password),
                ReplicaSet = model.ReplicaSet,
                ActiveCollection = model.ActiveCollection,
                CompletedCollection = model.CompletedCollection,
                WorkflowDefinitionCollection = model.WorkflowDefinitionCollection
            };
        }
    }

    public class ConnectionTestResponse
    {
        public bool Success { get; set; }

        public string? Message { get; set; }
    }

    public class ConnectionValidator
    {
        public virtual IReadOnlyList<string> Validate(ConnectionRequest request)
        {
            var errors = new List<string>();

            if (String.IsNullOrWhiteSpace(request.Host))
            {
                errors.Add("Host cannot be empty");
            }
            if (!request.Port.HasValue || request.Port.Value <= 0)
            {
                errors.Add("Port must have a numeric value");
            }
            if (String.IsNullOrWhiteSpace(request.Database))
            {
                errors.Add("Database cannot be empty");
            }
            // RavenDb collections are fixed by the store
            if (request.WorkflowStoreType == WorkflowStoreType.MongoDb)
            {
                if (String.IsNullOrWhiteSpace(request.ActiveCollection))
                {
                    errors.Add("Active collection cannot be empty");
                }
                if (String.IsNullOrWhiteSpace(request.CompletedCollection))
                {
                    errors.Add("Completed collection cannot be empty");
                }
            }
            if ((request.Password ?? String.Empty) != (request.PasswordConfirm ?? String.Empty))
            {
                errors.Add("Password and confirmation password do not match");
            }

            return errors;
        }
    }
}
