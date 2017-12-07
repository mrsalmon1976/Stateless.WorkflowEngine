using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Commands;
using NSubstitute;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using NUnit.Framework.Constraints;

namespace Test.Stateless.WorkflowEngine.Commands
{
    public class UnsuspendWorkflowCommandTest
    {
        private UnsuspendWorkflowCommand _command;

        [SetUp]
        public void UnsuspendWorkflowCommandTestSetUp()
        {
            this._command = new UnsuspendWorkflowCommand();
        }

        #region Execute Tests

        [Test]
        public void Execute_WorkflowDoesNotExist_RaisesException()
        {
            // arrange
            Guid workflowId = Guid.NewGuid();
            Workflow workflow = null;

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.Get(Arg.Any<Guid>()).Returns(workflow);

            _command.WorkflowStore = workflowStore;
            _command.WorkflowId = workflowId;

            // act
            TestDelegate del = () => _command.Execute();

            // assert
            Assert.Throws(typeof(CommandConfigurationException), del);
            
        }

        [Test]
        public void Execute_ValidWorkflow_SetsProperties()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = Guid.NewGuid();
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.UtcNow.AddMonths(1);

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.Get(workflow.Id).Returns(workflow);

            _command.WorkflowStore = workflowStore;
            _command.WorkflowId = workflow.Id;
            Workflow result = _command.Execute();

            Assert.IsFalse(workflow.IsSuspended);
            Assert.LessOrEqual(workflow.ResumeOn, DateTime.UtcNow);
            Assert.Greater(DateTime.UtcNow.AddSeconds(2), workflow.ResumeOn);
        }

        [Test]
        public void Execute_ValidWorkflow_ReturnsWorkflow()
        {
            BasicWorkflow workflow = new BasicWorkflow(BasicWorkflow.State.Start);
            workflow.Id = Guid.NewGuid();
            workflow.IsSuspended = true;
            workflow.ResumeOn = DateTime.UtcNow.AddMonths(1);

            IWorkflowStore workflowStore = Substitute.For<IWorkflowStore>();
            workflowStore.Get(workflow.Id).Returns(workflow);

            _command.WorkflowStore = workflowStore;
            _command.WorkflowId = workflow.Id;
            Workflow result = _command.Execute();

            Assert.AreEqual(workflow.Id, result.Id);
        }

        #endregion

        #region Validate Tests

        [Test]
        public void Validate_WorkflowStoreNull_RaisesException()
        {
            _command.WorkflowId = Guid.NewGuid();

            TestDelegate del = () => _command.Validate();

            Assert.Throws<CommandConfigurationException>(del);
        }

        [Test]
        public void Validate_WorkflowIdEmpty_RaisesException()
        {
            _command.WorkflowStore = Substitute.For<IWorkflowStore>();
            TestDelegate del = () => _command.Validate();
            Assert.Throws<CommandConfigurationException>(del);
        }

        #endregion
    }
}
