using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine.Exceptions;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;

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
    }
}
