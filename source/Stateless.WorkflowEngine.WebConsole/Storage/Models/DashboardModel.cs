namespace Stateless.WorkflowEngine.WebConsole.Storage.Models
{
    public class DashboardModel
    {
        public const string DefaultName = "Default";

        public Guid Id { get; set; } = Guid.NewGuid();

        public string? Name { get; set; }

        /// <summary>
        /// Gets/sets whether this is the default dashboard.  There is always exactly one default dashboard - it
        /// can be renamed but not deleted, and connections saved by previous versions are assigned to it.
        /// </summary>
        public bool IsDefault { get; set; }
    }
}
