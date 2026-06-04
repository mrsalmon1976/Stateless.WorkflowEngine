using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stateless.WorkflowEngine.Exceptions;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection;
using Test.Stateless.WorkflowEngine.Workflows.Async;
using Test.Stateless.WorkflowEngine.Workflows.Async.Actions;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;
using NSubstitute;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowTest
    {
        #region ExecuteWorkflowAction Tests

        [TestCase("")]
        [TestCase(null)]
        public void Fire_WithNullOrEmptyTriggerName_ThrowsException(string triggerName)
        {
            SimpleTwoStateWorkflow wf = new SimpleTwoStateWorkflow(SimpleTwoStateWorkflow.State.Start);
            TestDelegate del = () => wf.Fire(triggerName);
            Assert.Throws<WorkflowException>(del);
        }

        [Test]
        public void ExecuteWorkflowAction_ActionWithoutDefaultConstructorAndNoDependencyResolver_ThrowsMissingMethodException()
        {
            DependencyInjectionWorkflow wf = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            wf.DependencyResolver = null;
            TestDelegate del = () => wf.Fire(DependencyInjectionWorkflow.Trigger.DoStuff);
            Assert.Throws<MissingMethodException>(del);
        }

        [Test]
        public void ExecuteWorkflowAction_WithDependencyResolver_UsesResolver()
        {
            IWorkflowEngineDependencyResolver resolver = Substitute.For<IWorkflowEngineDependencyResolver>();
            resolver.GetInstance<NoDefaultConstructorAction>().Returns(new NoDefaultConstructorAction("test", 1));
            DependencyInjectionWorkflow wf = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            wf.DependencyResolver = resolver;
            wf.Fire(DependencyInjectionWorkflow.Trigger.DoStuff);
            resolver.Received(1).GetInstance<NoDefaultConstructorAction>();
        }

        #endregion

        #region ExecuteWorkflowActionAsync Tests

        [Test]
        public async Task ExecuteWorkflowActionAsync_ExecutesAction()
        {
            AsyncDoingStuffAction action = new AsyncDoingStuffAction();
            IWorkflowEngineDependencyResolver resolver = Substitute.For<IWorkflowEngineDependencyResolver>();
            resolver.GetInstance<AsyncDoingStuffAction>().Returns(action);

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.DependencyResolver = resolver;
            await wf.RunActionAsync<AsyncDoingStuffAction>();

            Assert.That(action.WasExecuted, Is.True);
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_HooksAndActionCalledInOrder()
        {
            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            await wf.RunActionAsync<TestableAsyncWorkflow.OrderTrackingAction>();

            Assert.That(wf.CallOrder, Is.EqualTo(new List<string> { "executing", "action", "executed" }));
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_WithDependencyResolver_UsesResolver()
        {
            IWorkflowEngineDependencyResolver resolver = Substitute.For<IWorkflowEngineDependencyResolver>();
            resolver.GetInstance<AsyncDoingStuffAction>().Returns(new AsyncDoingStuffAction());

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.DependencyResolver = resolver;
            await wf.RunActionAsync<AsyncDoingStuffAction>();

            resolver.Received(1).GetInstance<AsyncDoingStuffAction>();
        }

        [Test]
        public void ExecuteWorkflowActionAsync_WithoutDefaultConstructorAndNoDependencyResolver_ThrowsMissingMethodException()
        {
            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.DependencyResolver = null;
            TestDelegate del = () => wf.RunActionAsync<NoDefaultConstructorAsyncAction>().GetAwaiter().GetResult();
            Assert.Throws<MissingMethodException>(del);
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_PassesCancellationTokenToAction()
        {
            AsyncDoingStuffAction action = new AsyncDoingStuffAction();
            IWorkflowEngineDependencyResolver resolver = Substitute.For<IWorkflowEngineDependencyResolver>();
            resolver.GetInstance<AsyncDoingStuffAction>().Returns(action);

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.DependencyResolver = resolver;

            CancellationToken expectedToken = new CancellationTokenSource().Token;
            await wf.RunActionAsync<AsyncDoingStuffAction>(expectedToken);

            Assert.That(action.ReceivedToken, Is.EqualTo(expectedToken));
        }

        #endregion

    }
}
