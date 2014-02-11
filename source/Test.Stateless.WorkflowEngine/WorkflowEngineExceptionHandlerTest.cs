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

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowEngineExceptionHandlerTest
    {
        [Test]
        public void HandleMultipleInstanceWorkflowException_RetryCountNotExceeded_SetsResumeOnToFuture()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleMultipleInstanceWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow);
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(10));
            Assert.IsFalse(workflow.IsSuspended);
        }

        [Test]
        public void HandleMultipleInstanceWorkflowException_RetryCountExceeded_SuspendsWorkflow()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };
            workflow.RetryCount = workflow.RetryIntervals.Length;

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleMultipleInstanceWorkflowException(workflow, ex);

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.AreEqual(DateTime.MinValue, workflow.ResumeOn);
            Assert.IsTrue(workflow.IsSuspended);
        }

        [Test]
        public void HandleSingleInstanceWorkflowException_RetryIntervalLengthNotExceeded_SleepsForSpecifiedTime()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleSingleInstanceWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow);
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(10));
            Assert.IsFalse(workflow.IsSuspended);
        }

        [TestCase(3)]
        [TestCase(7)]
        public void HandleSingleInstanceWorkflowException_RetryIntervalLengthNotExceeded_SleepsForSpecifiedTime(int retryCount)
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] {  };
            workflow.RetryCount = retryCount;

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleSingleInstanceWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.AreEqual(ex.ToString(), workflow.LastException);

            int approximateSeconds = retryCount * 60;
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow.AddSeconds(approximateSeconds - 1));
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(approximateSeconds + 10));
            Assert.IsFalse(workflow.IsSuspended);
        }

    }
}
