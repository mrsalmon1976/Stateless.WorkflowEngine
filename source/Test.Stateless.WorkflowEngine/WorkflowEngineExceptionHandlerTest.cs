using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NSubstitute;
using Stateless.WorkflowEngine.Stores;
using Test.Stateless.WorkflowEngine.Workflows.Broken;
using Stateless.WorkflowEngine;
using System.Threading;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Stateless.WorkflowEngine.Exceptions;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowEngineExceptionHandlerTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void HandleWorkflowException_RetryIntervalEmpty_ThrowsException(bool isSingleInstance)
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { };
            workflow.IsSingleInstance = isSingleInstance;

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            TestDelegate del = () => exceptionHandler.HandleWorkflowException(workflow, new Exception("Dummy exception"));

            // assert
            Assert.Throws<WorkflowException>(del);
        }

        [Test]
        public void HandleWorkflowException_RetryCountNotExceeded_SetsResumeOnToFuture()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.That(workflow.LastException, Is.EqualTo(ex.ToString()));
            Assert.That(workflow.ResumeOn, Is.GreaterThan(DateTime.UtcNow));
            Assert.That(workflow.ResumeOn, Is.LessThan(DateTime.UtcNow.AddSeconds(10)));
            Assert.That(workflow.IsSuspended, Is.False);
        }

        [Test]
        public void HandleWorkflowException_RetryCountExceeded_SuspendsWorkflow()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };
            workflow.RetryCount = workflow.RetryIntervals.Length;

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleWorkflowException(workflow, ex);

            Assert.That(workflow.LastException, Is.EqualTo(ex.ToString()));
            Assert.That(workflow.ResumeOn, Is.EqualTo(DateTime.MinValue));
            Assert.That(workflow.IsSuspended, Is.True);
        }
    }
}
