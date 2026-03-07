using Stateless.WorkflowEngine.Exceptions;
using System;

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
                workflow.RetryCount += 1;
            }
            else
            {
                // we're out of tries - suspend
                workflow.IsSuspended = true;
            }
        }


    }
}
