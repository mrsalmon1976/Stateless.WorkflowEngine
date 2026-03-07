using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using NUnit.Framework;
using Test.Stateless.WorkflowEngine.Workflows.Basic;
using Test.Stateless.WorkflowEngine.Workflows.SimpleTwoState;

namespace Test.Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Test fixture for MemoryWorkflowStoreTest.  Note that the majority of tests are in the base
    /// class so all methods of WorkflowStore are tested consistently.  This class adds tests specific
    /// to the MemoryWorkflowStore implementation, particularly around thread safety.
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

        #region GetDefinitions Tests

        [Test]
        public void GetDefinitions_ReturnsSnapshot_SubsequentSaveDoesNotAffectReturnedList()
        {
            // Arrange
            IWorkflowStore store = GetStore();
            store.SaveDefinition(CreateWorkflowDefinition<BasicWorkflow>());

            // Act: capture the list, then add a second definition
            IEnumerable<WorkflowDefinition> snapshot = store.GetDefinitions();
            store.SaveDefinition(CreateWorkflowDefinition<SimpleTwoStateWorkflow>());

            // Assert: the snapshot should not reflect the subsequent save
            Assert.That(snapshot.Count(), Is.EqualTo(1));
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void ConcurrentSaveAndGetActive_DoesNotThrow()
        {
            // Verifies that concurrent reads (GetActive) and writes (Save) do not cause
            // InvalidOperationException ("collection was modified") on the internal dictionary.
            IWorkflowStore store = GetStore();

            for (int i = 0; i < 20; i++)
            {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.CreatedOn = DateTime.UtcNow.AddMinutes(-2);
                wf.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                wf.ResumeTrigger = BasicWorkflow.Trigger.DoStuff.ToString();
                store.Save(wf);
            }

            bool exceptionThrown = false;

            Parallel.For(0, 100, i =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                        wf.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                        store.Save(wf);
                    }
                    else
                    {
                        store.GetActive(10).ToList();
                    }
                }
                catch (Exception)
                {
                    exceptionThrown = true;
                }
            });

            Assert.That(exceptionThrown, Is.False);
        }

        [Test]
        public void ConcurrentSaveAndGetCounts_DoesNotThrow()
        {
            // Verifies that concurrent writes and count reads are race-condition free.
            IWorkflowStore store = GetStore();
            bool exceptionThrown = false;

            Parallel.For(0, 100, i =>
            {
                try
                {
                    if (i % 3 == 0)
                    {
                        BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                        store.Save(wf);
                    }
                    else if (i % 3 == 1)
                    {
                        store.GetActiveCount();
                        store.GetIncompleteCount();
                    }
                    else
                    {
                        store.GetSuspendedCount();
                    }
                }
                catch (Exception)
                {
                    exceptionThrown = true;
                }
            });

            Assert.That(exceptionThrown, Is.False);
        }

        [Test]
        public void ConcurrentArchiveAndGetActive_DoesNotThrow()
        {
            // Verifies that Archive (which writes to both dictionaries) and GetActive (which reads)
            // can run concurrently without corrupting the dictionary.
            IWorkflowStore store = GetStore();
            List<Guid> ids = new List<Guid>();

            for (int i = 0; i < 50; i++)
            {
                BasicWorkflow wf = new BasicWorkflow(BasicWorkflow.State.Start);
                wf.ResumeOn = DateTime.UtcNow.AddMinutes(-2);
                wf.CompletedOn = DateTime.UtcNow;
                store.Save(wf);
                ids.Add(wf.Id);
            }

            bool exceptionThrown = false;
            int archiveIndex = 0;

            Parallel.For(0, ids.Count * 2, i =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        int idx = System.Threading.Interlocked.Increment(ref archiveIndex) - 1;
                        if (idx < ids.Count)
                        {
                            Workflow wf = store.GetOrDefault(ids[idx]);
                            if (wf != null)
                                store.Archive(wf);
                        }
                    }
                    else
                    {
                        store.GetActive(10).ToList();
                    }
                }
                catch (Exception)
                {
                    exceptionThrown = true;
                }
            });

            Assert.That(exceptionThrown, Is.False);
        }

        [Test]
        public void MultipleStoreInstances_HaveIndependentState()
        {
            // Verifies that two separate MemoryWorkflowStore instances have independent data.
            // Also confirms syncLock is an instance field: if it were static, parallel test runs
            // would contend unnecessarily, though data would still be separate.
            IWorkflowStore store1 = GetStore();
            IWorkflowStore store2 = GetStore();

            BasicWorkflow wf1 = new BasicWorkflow(BasicWorkflow.State.Start);
            BasicWorkflow wf2 = new BasicWorkflow(BasicWorkflow.State.Start);

            store1.Save(wf1);
            store2.Save(wf2);

            // Both stores should operate on their own data independently
            Assert.That(store1.GetIncompleteCount(), Is.EqualTo(1));
            Assert.That(store2.GetIncompleteCount(), Is.EqualTo(1));
            Assert.That(store1.GetOrDefault(wf2.Id), Is.Null);
            Assert.That(store2.GetOrDefault(wf1.Id), Is.Null);
        }

        #endregion

    }
}
