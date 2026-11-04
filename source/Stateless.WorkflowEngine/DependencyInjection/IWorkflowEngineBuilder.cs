namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Configures the workflow engine services added by <c>AddStatelessWorkflowEngine</c> - the store, workflow types
    /// and workflow actions.
    /// </summary>
    public interface IWorkflowEngineBuilder
    {
        IServiceCollection Services { get; }
    }

    internal sealed class WorkflowEngineBuilder : IWorkflowEngineBuilder
    {
        public WorkflowEngineBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
