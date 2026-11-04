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
using Microsoft.Extensions.DependencyInjection;

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
            Action del = () => wf.Fire(triggerName);
            Assert.Throws<WorkflowException>(del);
        }

        [Test]
        public void ExecuteWorkflowAction_ActionWithoutDefaultConstructorAndNoServiceProvider_ThrowsMissingMethodException()
        {
            DependencyInjectionWorkflow wf = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            wf.ServiceProvider = null;
            Action del = () => wf.Fire(DependencyInjectionWorkflow.Trigger.DoStuff);
            Assert.Throws<MissingMethodException>(del);
        }

        [Test]
        public void ExecuteWorkflowAction_WithServiceProvider_ActionResolvedFromServiceProvider()
        {
            int created = 0;
            using ServiceProvider serviceProvider = new ServiceCollection()
                .AddTransient(sp => { created++; return new NoDefaultConstructorAction("test", 1); })
                .BuildServiceProvider();
            DependencyInjectionWorkflow wf = new DependencyInjectionWorkflow(DependencyInjectionWorkflow.State.Start);
            wf.ServiceProvider = serviceProvider;
            wf.Fire(DependencyInjectionWorkflow.Trigger.DoStuff);
            Assert.That(created, Is.EqualTo(1));
        }

        #endregion

        #region ExecuteWorkflowActionAsync Tests

        [Test]
        public async Task ExecuteWorkflowActionAsync_ExecutesAction()
        {
            AsyncDoingStuffAction action = new AsyncDoingStuffAction();
            using ServiceProvider serviceProvider = ProviderWith(action);

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.ServiceProvider = serviceProvider;
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
        public async Task ExecuteWorkflowActionAsync_WithServiceProvider_ActionResolvedFromServiceProvider()
        {
            int created = 0;
            using ServiceProvider serviceProvider = new ServiceCollection()
                .AddTransient(sp => { created++; return new NoDefaultConstructorAsyncAction("test"); })
                .BuildServiceProvider();

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.ServiceProvider = serviceProvider;
            await wf.RunActionAsync<NoDefaultConstructorAsyncAction>();

            Assert.That(created, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteWorkflowActionAsync_WithoutDefaultConstructorAndNoServiceProvider_ThrowsMissingMethodException()
        {
            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.ServiceProvider = null;
            Action del = () => wf.RunActionAsync<NoDefaultConstructorAsyncAction>().GetAwaiter().GetResult();
            Assert.Throws<MissingMethodException>(del);
        }

        [Test]
        public async Task ExecuteWorkflowActionAsync_PassesCancellationTokenToAction()
        {
            AsyncDoingStuffAction action = new AsyncDoingStuffAction();
            using ServiceProvider serviceProvider = ProviderWith(action);

            TestableAsyncWorkflow wf = new TestableAsyncWorkflow();
            wf.ServiceProvider = serviceProvider;

            CancellationToken expectedToken = new CancellationTokenSource().Token;
            await wf.RunActionAsync<AsyncDoingStuffAction>(expectedToken);

            Assert.That(action.ReceivedToken, Is.EqualTo(expectedToken));
        }

        #endregion

        private static ServiceProvider ProviderWith<T>(T action) where T : class
        {
            return new ServiceCollection().AddSingleton(action).BuildServiceProvider();
        }

    }
}
