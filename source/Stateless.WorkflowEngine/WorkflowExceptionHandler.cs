using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.WorkflowEngine
{
    public interface IWorkflowExceptionHandler
    {
        void HandleMultipleInstanceWorkflowException(Workflow workflow, Exception exception);
        void HandleSingleInstanceWorkflowException(Workflow workflow, Exception exception);

    }

    public class WorkflowExceptionHandler : IWorkflowExceptionHandler
    {
        public void HandleMultipleInstanceWorkflowException(Workflow workflow, Exception exception)
        {
            workflow.LastException = exception.ToString();

            // if an error occurred running the workflow, we need to set a resume trigger
            if (workflow.RetryIntervals.Length > workflow.RetryCount)
            {
                workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryIntervals[workflow.RetryCount]);
            }
            else
            {
                // we've run out of retry intervals, suspend the workflow
                workflow.IsSuspended = true;
                workflow.OnSuspend();
            }

        }

        public void HandleSingleInstanceWorkflowException(Workflow workflow, Exception exception)
        {
            workflow.LastException = exception.ToString();

            if (workflow.RetryIntervals.Length > workflow.RetryCount)
            {
                workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryIntervals[workflow.RetryCount]);
            }
            else
            {
                // we've exceeded the RetryIntervals specified on the workflow but it's single instance so we can't 
                // suspend it....so just keep retrying in every increasing times.
                workflow.ResumeOn = DateTime.UtcNow.AddSeconds(workflow.RetryCount * 60);
            }
        }

    }
}
