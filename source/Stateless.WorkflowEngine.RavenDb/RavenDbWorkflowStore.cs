using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Raven.Client;
using Stateless.WorkflowEngine.Models;
using Stateless.WorkflowEngine.Stores;

namespace Stateless.WorkflowEngine.RavenDb
{
    /// <summary>
    /// Stores workflows in Raven Db.  
    /// </summary>
    public class RavenDbWorkflowStore : WorkflowStore
    {
        private readonly IDocumentStore _documentStore;
        private readonly string _database;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="documentStore"></param>
        /// <param name="database">The RavenDb database name.  For embedded databases, specify null or empty string.</param>
        public RavenDbWorkflowStore(IDocumentStore documentStore, string database)
        {
            this._documentStore = documentStore;
            this._database = database;
        }

        private IDocumentSession OpenSession()
        {
            // if no database is specified, we're assuming it's embedded so we can't use the 
            // override.  If it's embedded, make sure we wait for indexes after saves for testing purposes.
            if (String.IsNullOrWhiteSpace(this._database))
            {
                var session = this._documentStore.OpenSession();
                session.Advanced.WaitForIndexesAfterSaveChanges();
                return session;
            }
            return this._documentStore.OpenSession(this._database);
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                WorkflowContainer wf = session.Load<WorkflowContainer>(workflow.Id);
                session.Delete<WorkflowContainer>(wf);

                session.Store(new CompletedWorkflow(wf.Workflow));

                session.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection. 
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override void Delete(Guid id)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                WorkflowContainer wf = session.Load<WorkflowContainer>(id);
                session.Delete<WorkflowContainer>(wf);
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetIncompleteCount()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<WorkflowContainer>().Where(x => x.Workflow.IsSuspended == false).Count();
            }
        }

        /// <summary>
        /// Gets all workflows of a specified type.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return from s in session.Query<WorkflowContainer>()
                    .Where(x => x.WorkflowType == workflowType)
                    .OrderByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    select s.Workflow;
            }
        }

        /// <summary>
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override long GetCompletedCount()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<CompletedWorkflow>().Count();
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
            using (IDocumentSession session = this.OpenSession())
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
            using (IDocumentSession session = this.OpenSession())
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
            using (IDocumentSession session = this.OpenSession())
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
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return from s in session.Query<WorkflowContainer>()
                    .Where(x => x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                       select s.Workflow;
            }
        }

        /// <summary>
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override long GetSuspendedCount()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<WorkflowContainer>().Where(x => x.Workflow.IsSuspended == true).Count();
            }
        }


        /// <summary>
        /// Gives the opportunity for the workflow store to register a workflow type.  This may not always be necessary 
        /// on the store, but some applications require specific type registration (e.g. MongoDb).
        /// </summary>
        public override void RegisterType(Type t)
        {
            // no registration needed
        }

        /// <summary>
        /// Stores a new workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public override void Save(Workflow workflow)
        {
            using (IDocumentSession session = this.OpenSession())
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
            using (IDocumentSession session = this.OpenSession())
            {
                foreach (Workflow w in workflows)
                {
                    session.Store(new WorkflowContainer(w));
                }
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            Workflow w = this.Get(id);
            w.IsSuspended = true;
            this.Save(w);
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and 
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            Workflow w = this.Get(id);
            w.IsSuspended = false;
            w.RetryCount = 0;
            w.ResumeOn = DateTime.UtcNow;
            this.Save(w);
        }


    }
}
