﻿using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.RavenDb.Index;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Stateless.WorkflowEngine.RavenDb
{
    /// <summary>
    /// Stores workflows in Raven Db.  
    /// </summary>
    public class RavenDbWorkflowStore : WorkflowStore
    {
        private readonly IDocumentStore _documentStore;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="documentStore"></param>
        /// <param name="database">The RavenDb database name.  For embedded databases, specify null or empty string.</param>
        public RavenDbWorkflowStore(IDocumentStore documentStore)
        {
            this._documentStore = documentStore;
        }

        private IDocumentSession OpenSession()
        {
            var session = this._documentStore.OpenSession();
            session.Advanced.WaitForIndexesAfterSaveChanges();
            return session;
        }

        /// <summary>
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override void Archive(Workflow workflow)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
                RavenWorkflow wf = session.Load<RavenWorkflow>(fid);
                session.Delete<RavenWorkflow>(wf);

                session.Store(new RavenCompletedWorkflow(wf.Workflow));

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
                string fid = RavenDbIdUtility.FormatWorkflowId(id);
                RavenWorkflow wf = session.Load<RavenWorkflow>(fid);
                session.Delete<RavenWorkflow>(wf);
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override long GetActiveCount()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<RavenWorkflow>().Where(x => x.Workflow.IsSuspended == false).Count();
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
                return session.Query<RavenWorkflow>().Count();
            }
        }

        /// <summary>
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetAllByType(string workflowType)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.WorkflowType == workflowType)
                    .OrderBy(x => x.Workflow.CreatedOn)
                    select s.Workflow).ToList();
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
                return session.Query<RavenCompletedWorkflow>().Count();
            }
        }

        /// <summary>
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetCompletedOrDefault(Guid id)
        {
            string cid = RavenDbIdUtility.FormatCompletedWorkflowId(id);
            Workflow w = null;
            using (IDocumentSession session = this.OpenSession())
            {
                var rcw = session.Load<RavenCompletedWorkflow>(cid);
                if (rcw != null)
                {
                    w = rcw.Workflow;
                }
            }
            return w;
        }

        /// <summary>
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override WorkflowDefinition GetDefinitionByQualifiedName(string qualifiedName)
        {
            using (IDocumentSession session = this.OpenSession())
            {

                var result = from s in session.Query<RavenWorkflowDefinition>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => x.WorkflowDefinition.QualifiedName == qualifiedName)
                             select s.WorkflowDefinition;
                return result.SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<WorkflowDefinition> GetDefinitions()
        {
            using (IDocumentSession session = this.OpenSession())
            {

                var result = from s in session.Query<RavenWorkflowDefinition>()
                    .Customize(x => x.WaitForNonStaleResults())
                             select s.WorkflowDefinition;
                return result.ToList();
            }
        }

        /// <summary>
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public override Workflow GetOrDefault(Guid id)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            Workflow workflow = null;
            using (IDocumentSession session = this.OpenSession())
            {
                RavenWorkflow wc = session.Load<RavenWorkflow>(fid);
                if (wc != null)
                {
                    workflow = wc.Workflow;
                }
            }
            return workflow;
        }

        /// <summary>
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetActive(int count)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.IsSuspended == false && x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.Priority)
                    .ThenByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                    select s.Workflow).ToList();
            }
        }

        /// <summary>
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<Workflow> GetIncomplete(int count)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.Priority)
                    .ThenByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                       select s.Workflow).ToList();
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
                return session.Query<RavenWorkflow>().Where(x => x.Workflow.IsSuspended == true).Count();
            }
        }


        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            if (autoCreateIndexes)
            {
                // ravdendb indexes are safe by default, so we can just fire this off every time
                // https://ravendb.net/docs/article-page/4.2/csharp/indexes/creating-and-deploying
                new WorkflowIndex_Priority_RetryCount_CreatedOn().Execute(_documentStore);
                new CompletedWorkflowIndex_CreatedOn().Execute(_documentStore);
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
                RavenWorkflow wc = new RavenWorkflow(workflow);
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
                    session.Store(new RavenWorkflow(w));
                }
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override void SaveDefinition(WorkflowDefinition workflowDefinition)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                session.Store(new RavenWorkflowDefinition(workflowDefinition));
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
