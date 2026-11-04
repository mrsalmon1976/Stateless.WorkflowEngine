using System;

namespace Stateless.WorkflowEngine.DependencyInjection
{
    /// <summary>
    /// A workflow type added with <c>AddWorkflowType&lt;T&gt;()</c>, registered with the server when it is created.
    /// </summary>
    internal sealed class WorkflowTypeRegistration
    {
        private readonly Action<IWorkflowServer> _register;

        public WorkflowTypeRegistration(Type workflowType, Action<IWorkflowServer> register)
        {
            this.WorkflowType = workflowType;
            _register = register;
        }

        public Type WorkflowType { get; }

        public void Register(IWorkflowServer workflowServer)
        {
            _register(workflowServer);
        }
    }
}
