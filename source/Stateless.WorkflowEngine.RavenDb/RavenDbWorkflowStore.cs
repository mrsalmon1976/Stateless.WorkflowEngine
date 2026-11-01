using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Sparrow.Json;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.RavenDb
{
    /// <summary>
    /// Stores workflows in Raven Db.  
    /// </summary>
    public class RavenDbWorkflowStore : WorkflowStore
    {
        private readonly IDocumentStore _documentStore;

        private string WorkflowCollection => _documentStore.Conventions.FindCollectionName(typeof(RavenWorkflow));

        /// <summary>
        /// Gets the document store associated with the store.
        /// </summary>
        public IDocumentStore DocumentStore => _documentStore;

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

        private IAsyncDocumentSession OpenAsyncSession()
        {
            var session = this._documentStore.OpenAsyncSession();
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
        /// Archives a workflow, moving it into the completed store.
        /// </summary>
        /// <param name="workflow">The workflow to archive.</param>
        public override async Task ArchiveAsync(Workflow workflow)
        {
            using (var session = this.OpenAsyncSession())
            {
                string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
                RavenWorkflow wf = await session.LoadAsync<RavenWorkflow>(fid);
                session.Delete(wf);

                await session.StoreAsync(new RavenCompletedWorkflow(wf.Workflow));

                await session.SaveChangesAsync();
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
                // deleted by id so the workflow type doesn't need to be loaded (e.g. by the web console)
                session.Delete(RavenDbIdUtility.FormatWorkflowId(id));
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes a workflow from the active database store/collection.
        /// </summary>
        /// <param name="id">The workflow id.</param>
        public override async Task DeleteAsync(Guid id)
        {
            using (var session = this.OpenAsyncSession())
            {
                session.Delete(RavenDbIdUtility.FormatWorkflowId(id));
                await session.SaveChangesAsync();
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
        /// Gets the count of active workflows in the active collection (excluding suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetActiveCountAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                return await session.Query<RavenWorkflow>().Where(x => x.Workflow.IsSuspended == false).CountAsync();
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
        /// Gets the count of active workflows in the active collection (including suspended workflows).
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetIncompleteCountAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                return await session.Query<RavenWorkflow>().CountAsync();
            }
        }

		/// <summary>
		/// Gets all workflows of a specified fully qualified name ordered by create date.
		/// </summary>
		/// <returns></returns>
		public override IEnumerable<Workflow> GetAllByQualifiedName(string qualifiedName)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.QualifiedName == qualifiedName)
                    .OrderBy(x => x.Workflow.CreatedOn)
                    select s.Workflow).ToList();
            }
        }

        /// <summary>
        /// Gets all workflows of a specified fully qualified name ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByQualifiedNameAsync(string qualifiedName)
        {
            using (var session = this.OpenAsyncSession())
            {
                return await (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.QualifiedName == qualifiedName)
                    .OrderBy(x => x.Workflow.CreatedOn)
                    select s.Workflow).ToListAsync();
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
        /// Gets all incomplete workflows of a specified type ordered by create date.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetAllByTypeAsync(string workflowType)
        {
            using (var session = this.OpenAsyncSession())
            {
                return await (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.WorkflowType == workflowType)
                    .OrderBy(x => x.Workflow.CreatedOn)
                    select s.Workflow).ToListAsync();
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
        /// Gets the count of completed workflows in the completed collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetCompletedCountAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                return await session.Query<RavenCompletedWorkflow>().CountAsync();
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
        /// Gets a completed workflow by it's unique identifier, or null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetCompletedOrDefaultAsync(Guid id)
        {
            string cid = RavenDbIdUtility.FormatCompletedWorkflowId(id);
            using (var session = this.OpenAsyncSession())
            {
                var rcw = await session.LoadAsync<RavenCompletedWorkflow>(cid);
                return rcw?.Workflow;
            }
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
        /// Gets the first <c>count</c> incomplete workflows as raw JSON documents.  The documents are not deserialized,
        /// so the workflow types don't need to be loaded (e.g. by the web console).
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override IEnumerable<string> GetIncompleteWorkflowsAsJson(int count)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                // the documents belong to the session, so they must be converted before it is disposed
                return session.Advanced.RawQuery<BlittableJsonReaderObject>($"from '{WorkflowCollection}'")
                    .NoTracking()
                    .Take(count)
                    .ToList()
                    .Select(x => x.ToString())
                    .ToList();
            }
        }

        /// <summary>
        /// Gets an active workflow as a raw JSON document, or null if it does not exist.  The document is not
        /// deserialized, so the workflow type doesn't need to be loaded (e.g. by the web console).
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override string GetWorkflowAsJson(Guid id)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                BlittableJsonReaderObject doc = session.Load<BlittableJsonReaderObject>(RavenDbIdUtility.FormatWorkflowId(id));
                return doc?.ToString();
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
        /// Gets an active workflow by it's unique identifier, returning null if it does not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<Workflow> GetOrDefaultAsync(Guid id)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            using (var session = this.OpenAsyncSession())
            {
                RavenWorkflow wc = await session.LoadAsync<RavenWorkflow>(fid);
                return wc?.Workflow;
            }
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
        /// Gets the first <c>count</c> active workflows, ordered by Priority, RetryCount, and then CreationDate.
        /// Note that is the primary method used by the workflow engine to fetch workflows.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetActiveAsync(int count)
        {
            using (var session = this.OpenAsyncSession())
            {
                return await (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.IsSuspended == false && x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.Priority)
                    .ThenByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                        select s.Workflow).ToListAsync();
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
        /// Gets the first <c>count</c> incomplete workflows (including suspended), ordered by Priority, then RetryCount, and then CreationDate.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<Workflow>> GetIncompleteAsync(int count)
        {
            using (var session = this.OpenAsyncSession())
            {
                return await (from s in session.Query<RavenWorkflow>()
                    .Where(x => x.Workflow.ResumeOn <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Workflow.Priority)
                    .ThenByDescending(x => x.Workflow.RetryCount)
                    .ThenBy(x => x.Workflow.CreatedOn)
                    .Take(count)
                    select s.Workflow).ToListAsync();
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
        /// Gets the count of suspended workflows in the active collection.
        /// </summary>
        /// <returns></returns>
        public override async Task<long> GetSuspendedCountAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                return await session.Query<RavenWorkflow>().Where(x => x.Workflow.IsSuspended == true).CountAsync();
            }
        }


        /// <summary>
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override void Initialise(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            // ensure the database exists
            if (autoCreateTables || autoCreateIndexes)
            {
                try
                {
                    _documentStore.Maintenance.Server.Send(
                        new CreateDatabaseOperation(new DatabaseRecord(_documentStore.Database)));
                }
                catch (ConcurrencyException)
                {
                    // database already exists
                }
            }

            // No static indexes are created.  All queries in this store are dynamic queries (session.Query<T>() without
            // an index), which RavenDB only ever answers from auto indexes - it creates an auto index for each query
            // shape on first use (e.g. "Auto/RavenWorkflows/ByWorkflow.IsSuspended") and maintains it from then on.
            // Versions prior to 6.0 deployed static indexes here ("WorkflowIndex/Priority/RetryCount/CreatedOn" and
            // "CompletedWorkflowIndex/CreatedOn"), but no query ever used them, so they only added indexing and storage
            // overhead.  Existing copies can be deleted from the database.  See https://ravendb.net/docs/article-page/7.0/csharp/indexes/creating-and-deploying
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
        /// Stores a new workflow.
        /// </summary>
        /// <param name="workflow"></param>
        public override async Task SaveAsync(Workflow workflow)
        {
            using (var session = this.OpenAsyncSession())
            {
                RavenWorkflow wc = new RavenWorkflow(workflow);
                await session.StoreAsync(wc);
                await session.SaveChangesAsync();
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
        /// Stores a collection of new workflows.
        /// </summary>
        /// <param name="workflows">The workflows.</param>
        public override async Task SaveAsync(IEnumerable<Workflow> workflows)
        {
            using (var session = this.OpenAsyncSession())
            {
                foreach (Workflow w in workflows)
                {
                    await session.StoreAsync(new RavenWorkflow(w));
                }
                await session.SaveChangesAsync();
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
        /// Saves a workflow definition, based on its qualified name (Id will not be considered for the upsert).
        /// </summary>
        /// <param name="workflowDefinition"></param>
        public override async Task SaveDefinitionAsync(WorkflowDefinition workflowDefinition)
        {
            using (var session = this.OpenAsyncSession())
            {
                await session.StoreAsync(new RavenWorkflowDefinition(workflowDefinition));
                await session.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override void SuspendWorkflow(Guid id)
        {
            PatchWorkflow(id, SuspendScript, null);
        }

        /// <summary>
        /// Moves an active workflow into a suspended state.
        /// </summary>
        /// <param name="id"></param>
        public override async Task SuspendWorkflowAsync(Guid id)
        {
            await PatchWorkflowAsync(id, SuspendScript, null);
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override void UnsuspendWorkflow(Guid id)
        {
            PatchWorkflow(id, UnsuspendScript, new Dictionary<string, object> { { "resumeOn", DateTime.UtcNow } });
        }

        /// <summary>
        /// Moves a suspended workflow into an unsuspended state, but setting IsSuspended to false, and
        /// resetting the Resume Date and Retry Count.
        /// </summary>
        /// <param name="id"></param>
        public override async Task UnsuspendWorkflowAsync(Guid id)
        {
            await PatchWorkflowAsync(id, UnsuspendScript, new Dictionary<string, object> { { "resumeOn", DateTime.UtcNow } });
        }

        private const string SuspendScript = "this.Workflow.IsSuspended = true;";

        private const string UnsuspendScript = "this.Workflow.IsSuspended = false; this.Workflow.RetryCount = 0; this.Workflow.ResumeOn = args.resumeOn;";

        /// <summary>
        /// Patches the raw workflow document, so the workflow type doesn't need to be loaded (e.g. by the web console).
        /// </summary>
        private void PatchWorkflow(Guid id, string script, Dictionary<string, object> values)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            using (IDocumentSession session = this.OpenSession())
            {
                if (!session.Advanced.Exists(fid))
                {
                    throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
                }
                session.Advanced.Defer(new PatchCommandData(fid, null, new PatchRequest { Script = script, Values = values ?? new Dictionary<string, object>() }));
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Patches the raw workflow document, so the workflow type doesn't need to be loaded (e.g. by the web console).
        /// </summary>
        private async Task PatchWorkflowAsync(Guid id, string script, Dictionary<string, object> values)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            using (var session = this.OpenAsyncSession())
            {
                if (!await session.Advanced.ExistsAsync(fid))
                {
                    throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
                }
                session.Advanced.Defer(new PatchCommandData(fid, null, new PatchRequest { Script = script, Values = values ?? new Dictionary<string, object>() }));
                await session.SaveChangesAsync();
            }
        }


    }
}
