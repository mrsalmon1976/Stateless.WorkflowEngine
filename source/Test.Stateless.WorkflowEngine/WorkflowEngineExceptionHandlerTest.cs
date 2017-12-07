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
        public void HandleWorkflowException_MultipleInstanceRetryCountNotExceeded_SetsResumeOnToFuture()
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

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow);
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(10));
            Assert.IsFalse(workflow.IsSuspended);
        }

        [Test]
        public void HandleWorkflowException_MultipleInstanceRetryCountExceeded_SuspendsWorkflow()
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

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.AreEqual(DateTime.MinValue, workflow.ResumeOn);
            Assert.IsTrue(workflow.IsSuspended);
        }

        [Test]
        public void HandleWorkflowException_SingleInstanceRetryIntervalLengthNotExceeded_SleepsForSpecifiedTime()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 10, 10, 10 };
            workflow.IsSingleInstance = true;

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.AreEqual(ex.ToString(), workflow.LastException);
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow);
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(10));
            Assert.IsFalse(workflow.IsSuspended);
        }

        [TestCase(10, 3)]
        [TestCase(20, 7)]
        public void HandleWorkflowException_SingleInstanceRetryIntervalLengthNotExceeded_SleepsForSpecifiedTime(int lastRetryInterval, int retryCount)
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryIntervals = new int[] { 2, 5, lastRetryInterval };
            workflow.RetryCount = retryCount;
            workflow.IsSingleInstance = true;

            Exception ex = new Exception("Dummy exception");

            // execute
            IWorkflowExceptionHandler exceptionHandler = new WorkflowExceptionHandler();
            exceptionHandler.HandleWorkflowException(workflow, ex);
            Thread.Sleep(100);

            Assert.AreEqual(ex.ToString(), workflow.LastException);

            // we only have one retry interval on this workflow, 
            Assert.IsTrue(workflow.ResumeOn > DateTime.UtcNow.AddSeconds(lastRetryInterval - 1));
            Assert.IsTrue(workflow.ResumeOn < DateTime.UtcNow.AddSeconds(lastRetryInterval + 10));
            Assert.IsFalse(workflow.IsSuspended);
        }

    }
}
