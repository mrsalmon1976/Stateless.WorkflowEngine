using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Raven.Client;
using StructureMap;
using Stateless.WorkflowEngine.Models;

namespace Stateless.WorkflowEngine.Stores
{
    /// <summary>
    /// Stores workflows in Raven Db.  
    /// </summary>
    public class RavenDbWorkflowStore : WorkflowStore
    {
        public RavenDbWorkflowStore()
        {
            try
            {
                ObjectFactory.GetInstance<IDocumentStore>();
            }
            catch (Exception ex)
            {
                throw new WorkflowEngineException("An IDocumentStore needs to be configured before using the RavenDbWorkflowStore", ex);
            }
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                WorkflowContainer wf = session.Load<WorkflowContainer>(workflow.Id);
                session.Delete<WorkflowContainer>(wf);

                session.Store(new CompletedWorkflow(wf.Workflow));

                session.SaveChanges();
            }
        }

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                return from s in session.Query<WorkflowContainer>()
                    .Where(x => x.WorkflowType == workflowType)
                    .OrderByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    select s.Workflow;
            }
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override CompletedWorkflow GetCompletedOrDefault(Guid id)
        {
            CompletedWorkflow workflow = null;
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                workflow = session.Load<CompletedWorkflow>(id);
            }
            return workflow;
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetOrDefault(Guid id)
        {
            Workflow workflow = null;
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                WorkflowContainer wc = session.Load<WorkflowContainer>(id);
                if (wc != null)
                {
                    workflow = wc.Workflow;
                }
            }
            return workflow;
        }

        /// <summary>
        /// Gets the first <c>count</c> unsuspended active workflows, ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                return from s in session.Query<WorkflowContainer>()
                    .Where(x => x.Workflow.IsSuspended == false && x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                    select s.Workflow;
            }
        }

        /// <summary>
        /// Stores a new workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public override void Save(Workflow workflow)
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                WorkflowContainer wc = new WorkflowContainer(workflow);
                session.Store(wc);  
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Stores a collection of new workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override void Save(IEnumerable<Workflow> workflows)
        {
            using (IDocumentSession session = ObjectFactory.GetInstance<IDocumentSession>())
            {
                foreach (Workflow w in workflows)
                {
                    session.Store(new WorkflowContainer(w));
                }
                session.SaveChanges();
            }
        }

    }
}
