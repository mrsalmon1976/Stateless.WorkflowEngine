using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;

namespace Test.Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Test fixture for MemoryWorkflowStoreTest.  Note that this class should contain no tests - all the tests 
    /// are in the base class so all methods of WorkflowStore are tested consistently.
    /// </summary>
    [TestFixture]
    public class MemoryWorkflowStoreTest : WorkflowStoreTestBase
    {
        #region Protected Methods

        /// <summary>
        /// Gets the store relevant to the test.
        /// </summary>
        /// <returns></returns>
        protected override IWorkflowStore GetStore()
        {
            return new MemoryWorkflowStore();
        }

        #endregion

    }
}
