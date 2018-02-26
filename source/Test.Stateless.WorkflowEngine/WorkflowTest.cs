using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine.Exceptions;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection;
using Stateless.WorkflowEngine;
using Test.Stateless.WorkflowEngine.Workflows.DependencyInjection.Actions;
using NSubstitute;

namespace Test.Stateless.WorkflowEngine
{
    [TestFixture]
    public class WorkflowTest
    {
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

    }
}
