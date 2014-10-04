using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine.Commands
{
    public class UnsuspendWorkflowCommand : Command<Workflow>
    {
        public UnsuspendWorkflowCommand()
        {
        }

        public UnsuspendWorkflowCommand(IWorkflowStore workflowStore, Guid workflowId)
        {
            this.WorkflowStore = workflowStore;
            this.WorkflowId = workflowId;
        }

        public virtual Guid WorkflowId { get; set; }

        public virtual IWorkflowStore WorkflowStore { get; set; }

        public override Workflow Execute()
        {
            this.Validate();

            Workflow workflow = this.WorkflowStore.Get(this.WorkflowId);

            if (workflow == null)
            {
                throw new CommandConfigurationException(String.Format("Workflow not found matching id {0}", this.WorkflowId));
            }

            workflow.IsSuspended = false;
            workflow.ResumeOn = DateTime.UtcNow;

            return workflow;
        }

        public override void Validate()
        {
            if (this.WorkflowStore == null)
            {
                throw new CommandConfigurationException("Workflow store was not set");
            }
            if (this.WorkflowId == Guid.Empty)
            {
                throw new CommandConfigurationException("Workflow id property was not set");
            }
        }
    }
}
