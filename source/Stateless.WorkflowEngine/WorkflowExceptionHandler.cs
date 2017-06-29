using Stateless.WorkflowEngine.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowExceptionHandler
    {
        void HandleWorkflowException(Workflow workflow, Exception exception);

    }

    public class WorkflowExceptionHandler : IWorkflowExceptionHandler
    {

        public void HandleWorkflowException(Workflow workflow, Exception exception)
        {
            workflow.LastException = exception.ToString();

            if (workflow.RetryIntervals.Length == 0)
            {
                throw new WorkflowException("RetryInterval property of workflow contains no values");
            }

            // if an error occurred running the workflow, we need to set a resume trigger
            if (workflow.RetryIntervals.Length > workflow.RetryCount)
            {
                workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryIntervals[workflow.RetryCount]);
            }
            else
            {
                if (workflow.IsSingleInstance)
                {
                    // we can't suspend single instance workflows, but we've exceeded the RetryIntervals specified on the 
                    // workflow....so just keep retrying using the last interval
                    workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryIntervals[workflow.RetryIntervals.Length - 1]);
                }
                else
                {
                    // if the workflow is multiple instance, we've run out of retry intervalsso we suspend the workflow
                    workflow.IsSuspended = true;
                }
            }

        }


    }
}
