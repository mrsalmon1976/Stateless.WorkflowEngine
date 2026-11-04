using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Test.Stateless.WorkflowEngine.Logging;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.Broken;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowServerLoggingTest
    {
        private MemoryWorkflowStore _workflowStore;
        private ListLoggerFactory _loggerFactory;

        [SetUp]
        public void WorkflowServerLoggingTest_SetUp()
        {
            _workflowStore = new MemoryWorkflowStore();
            _loggerFactory = new ListLoggerFactory();
        }

        [Test]
        public void ExecuteWorkflow_ActionFails_LogsWarningWithRetry()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = new WorkflowServer(_workflowStore, null, _loggerFactory);
            workflowServer.ExecuteWorkflow(workflow);

            ListLoggerFactory.LogEntry entry = _loggerFactory.WithEventId(1003).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Category, Is.EqualTo(typeof(WorkflowServer).FullName));
            Assert.That(entry.Exception.Message, Is.EqualTo("This workflow is broken"));
            Assert.That(entry.Message, Does.Contain(workflow.Id.ToString()).And.Contain("retry 1"));
            Assert.That(_loggerFactory.WithEventId(1004), Is.Empty);
        }

        [Test]
        public void ExecuteWorkflow_ActionFailsOnLastRetry_LogsErrorSuspended()
        {
            BrokenWorkflow workflow = new BrokenWorkflow(BrokenWorkflow.State.Start);
            workflow.ResumeTrigger = BrokenWorkflow.Trigger.DoStuff.ToString();
            workflow.RetryCount = workflow.RetryIntervals.Length;
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = new WorkflowServer(_workflowStore, null, _loggerFactory);
            workflowServer.ExecuteWorkflow(workflow);

            ListLoggerFactory.LogEntry entry = _loggerFactory.WithEventId(1004).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception.Message, Is.EqualTo("This workflow is broken"));
            Assert.That(_loggerFactory.WithEventId(1003), Is.Empty);
        }

        [Test]
        public async Task ExecuteWorkflowAsync_WorkflowCompletes_LogsStepsAndCompletion()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = new WorkflowServer(_workflowStore, null, _loggerFactory);
            await workflowServer.ExecuteWorkflowAsync(workflow);

            Assert.That(_loggerFactory.WithEventId(1001).Select(x => x.Level), Is.EqualTo(new[] { LogLevel.Debug, LogLevel.Debug }));
            ListLoggerFactory.LogEntry entry = _loggerFactory.WithEventId(1002).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
            Assert.That(entry.Message, Does.Contain(workflow.Id.ToString()).And.Contain(nameof(BasicWorkflow)));
        }

        [Test]
        public void ExecuteWorkflows_WorkflowClaimedHandlerThrows_LogsWarningAndExecutesWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
            _workflowStore.Save(workflow);

            using WorkflowServer workflowServer = new WorkflowServer(_workflowStore, null, _loggerFactory);
            workflowServer.WorkflowClaimed += (sender, e) => throw new InvalidOperationException("handler failed");
            int executed = workflowServer.ExecuteWorkflows(1);

            Assert.That(executed, Is.EqualTo(1));
            ListLoggerFactory.LogEntry entry = _loggerFactory.WithEventId(1006).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Exception.Message, Is.EqualTo("handler failed"));
            Assert.That(_loggerFactory.WithEventId(1002).Count(), Is.EqualTo(1));
        }

        [Test]
        public void ExecuteWorkflows_ClaimFails_LogsError()
        {
            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.ClaimNext(Arg.Any<string>(), Arg.Any<int>()).Returns<IEnumerable<Workflow>>(x => throw new TimeoutException("claim failed"));

            using WorkflowServer workflowServer = new WorkflowServer(workflowStore, null, _loggerFactory);
            Assert.Throws<TimeoutException>(() => workflowServer.ExecuteWorkflows(1));

            ListLoggerFactory.LogEntry entry = _loggerFactory.WithEventId(1007).Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception.Message, Is.EqualTo("claim failed"));
        }

        [Test]
        public void Dispose_AfterExecutingWorkflows_LogsNodeRegisteredAndStopped()
        {
            WorkflowServer workflowServer = new WorkflowServer(_workflowStore, null, _loggerFactory);
            workflowServer.ExecuteWorkflows(1);
            string nodeId = workflowServer.NodeId;
            workflowServer.Dispose();

            ListLoggerFactory.LogEntry registered = _loggerFactory.WithEventId(1100).Single();
            Assert.That(registered.Level, Is.EqualTo(LogLevel.Information));
            Assert.That(registered.Category, Is.EqualTo("Stateless.WorkflowEngine.Services.WorkflowNodeMonitor"));
            Assert.That(registered.Message, Does.Contain(nodeId));
            Assert.That(_loggerFactory.WithEventId(1109).Single().Message, Does.Contain(nodeId));
        }
    }
}
