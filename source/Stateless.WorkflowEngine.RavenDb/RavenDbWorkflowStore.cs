using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.WorkflowEngine.Exceptions;
using Stateless.WorkflowEngine.Stores;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
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

        private string NodeCollection => _documentStore.Conventions.FindCollectionName(typeof(WorkflowNode));

        /// <summary>
        /// The number of candidate workflows fetched per claim attempt.  Candidates come from an index that may be stale,
        /// so some may already be claimed - the conditional patch skips them.
        /// </summary>
        private const int ClaimCandidateCount = 25;

        private const int MaxConcurrencyRetries = 3;

        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

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
        /// Gets a workflow by a qualified definition name.
        /// </summary>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public override async Task<WorkflowDefinition> GetDefinitionByQualifiedNameAsync(string qualifiedName)
        {
            using (var session = this.OpenAsyncSession())
            {
                var result = from s in session.Query<RavenWorkflowDefinition>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => x.WorkflowDefinition.QualifiedName == qualifiedName)
                             select s.WorkflowDefinition;
                return await result.SingleOrDefaultAsync();
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
        /// Gets all workflow definitions persisted in the store.
        /// </summary>
        /// <returns></returns>
        public override async Task<IEnumerable<WorkflowDefinition>> GetDefinitionsAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                var result = from s in session.Query<RavenWorkflowDefinition>()
                    .Customize(x => x.WaitForNonStaleResults())
                             select s.WorkflowDefinition;
                return await result.ToListAsync();
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
        /// Gets the first <c>count</c> incomplete workflows as raw JSON documents.  The documents are not deserialized,
        /// so the workflow types don't need to be loaded (e.g. by the web console).
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public override async Task<IEnumerable<string>> GetIncompleteWorkflowsAsJsonAsync(int count)
        {
            using (var session = this.OpenAsyncSession())
            {
                // the documents belong to the session, so they must be converted before it is disposed
                List<BlittableJsonReaderObject> docs = await session.Advanced.AsyncRawQuery<BlittableJsonReaderObject>($"from '{WorkflowCollection}'")
                    .NoTracking()
                    .Take(count)
                    .ToListAsync();
                return docs.Select(x => x.ToString()).ToList();
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
        /// Gets an active workflow as a raw JSON document, or null if it does not exist.  The document is not
        /// deserialized, so the workflow type doesn't need to be loaded (e.g. by the web console).
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public override async Task<string> GetWorkflowAsJsonAsync(Guid id)
        {
            using (var session = this.OpenAsyncSession())
            {
                BlittableJsonReaderObject doc = await session.LoadAsync<BlittableJsonReaderObject>(RavenDbIdUtility.FormatWorkflowId(id));
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
        /// Called to initialise the workflow store (creates tables/collections/indexes etc.)
        /// </summary>
        /// <param name="autoCreateTables"></param>
        /// <param name="autoCreateIndexes"></param>
        public override async Task InitialiseAsync(bool autoCreateTables, bool autoCreateIndexes, bool persistWorkflowDefinitions)
        {
            // ensure the database exists - no static indexes are created (see Initialise)
            if (autoCreateTables || autoCreateIndexes)
            {
                try
                {
                    await _documentStore.Maintenance.Server.SendAsync(
                        new CreateDatabaseOperation(new DatabaseRecord(_documentStore.Database)));
                }
                catch (ConcurrencyException)
                {
                    // database already exists
                }
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

        /// <summary>
        /// Marks an active workflow as complete and moves it into the completed collection, without executing any
        /// further steps.  The raw document is copied, so the workflow type doesn't need to be loaded (e.g. by the web
        /// console), and the copy and delete are saved in a single transaction.
        /// </summary>
        /// <param name="id"></param>
        public override void CompleteWorkflow(Guid id)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            using (IDocumentSession session = this.OpenSession())
            {
                if (!session.Advanced.Exists(fid))
                {
                    throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
                }
                session.Advanced.Defer(CompleteCommands(id));
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Marks an active workflow as complete and moves it into the completed collection, without executing any
        /// further steps.  The raw document is copied, so the workflow type doesn't need to be loaded (e.g. by the web
        /// console), and the copy and delete are saved in a single transaction.
        /// </summary>
        /// <param name="id"></param>
        public override async Task CompleteWorkflowAsync(Guid id)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            using (var session = this.OpenAsyncSession())
            {
                if (!await session.Advanced.ExistsAsync(fid))
                {
                    throw new WorkflowNotFoundException(String.Format("No workflow found matching id {0}", id));
                }
                session.Advanced.Defer(CompleteCommands(id));
                await session.SaveChangesAsync();
            }
        }

        // Copies the workflow into a completed workflow document, in the state the workflow server archives completed
        // workflows in.  The metadata puts the copy in the completed workflow collection.
        private const string CompleteScript = @"
var workflow = this.Workflow;
workflow.IsComplete = true;
workflow.CompletedOn = args.now;
workflow.LockedBy = null;
workflow.LockedOn = null;
put(args.completedId, { Workflow: workflow, WorkflowType: this.WorkflowType, '@metadata': { '@collection': args.collection, 'Raven-Clr-Type': args.clrType } });";

        private ICommandData[] CompleteCommands(Guid id)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(id);
            Type completedType = typeof(RavenCompletedWorkflow);
            PatchRequest patch = new PatchRequest
            {
                Script = CompleteScript,
                Values = new Dictionary<string, object>
                {
                    { "now", DateTime.UtcNow },
                    { "completedId", RavenDbIdUtility.FormatCompletedWorkflowId(id) },
                    { "collection", _documentStore.Conventions.FindCollectionName(completedType) },
                    { "clrType", _documentStore.Conventions.FindClrTypeName(completedType) }
                }
            };
            return new ICommandData[] { new PatchCommandData(fid, null, patch), new DeleteCommandData(fid, null) };
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

        // Claims a workflow only if it is still available.  Patches run atomically per document on the server, so if two
        // nodes patch the same document, only the first one changes it - the second gets PatchStatus.NotModified.  Dates
        // are compared as strings, which works as RavenDB stores them in a sortable ISO 8601 format.
        private const string ClaimScript = @"
if (this.Workflow.LockedBy == null && !this.Workflow.IsSuspended && this.Workflow.ResumeOn <= args.now) {
    this.Workflow.LockedBy = args.node;
    this.Workflow.LockedOn = args.now;
}";

        private const string HeartbeatScript = "this.LastHeartbeat = args.now;";

        /// <summary>
        /// Atomically claims the next available workflow for a node.  RavenDB has no find-and-modify, so candidates are
        /// read from the (possibly stale) index, and claimed with a conditional patch that only succeeds if the workflow
        /// is still available.
        /// </summary>
        public override Workflow ClaimNext(string nodeId)
        {
            return ClaimNext(nodeId, 1).FirstOrDefault();
        }

        /// <summary>
        /// Atomically claims the next available workflow for a node.  RavenDB has no find-and-modify, so candidates are
        /// read from the (possibly stale) index, and claimed with a conditional patch that only succeeds if the workflow
        /// is still available.
        /// </summary>
        public override async Task<Workflow> ClaimNextAsync(string nodeId)
        {
            return (await ClaimNextAsync(nodeId, 1)).FirstOrDefault();
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> workflows for a node.  The candidates are read from the (possibly stale)
        /// index in one query, each is claimed with a conditional patch that only succeeds if the workflow is still
        /// available, and the claimed workflows are loaded in one request.
        /// </summary>
        public override IEnumerable<Workflow> ClaimNext(string nodeId, int count)
        {
            List<Workflow> workflows = TryClaim(nodeId, count, waitForNonStaleResults: false, out bool retry);
            if (workflows.Count == 0 && retry)
            {
                // the index was behind (all the candidates were already claimed, or recently released workflows were
                // missing), so try again against an up-to-date index
                workflows = TryClaim(nodeId, count, waitForNonStaleResults: true, out retry);
            }
            return workflows;
        }

        /// <summary>
        /// Atomically claims up to <c>count</c> workflows for a node.  The candidates are read from the (possibly stale)
        /// index in one query, each is claimed with a conditional patch that only succeeds if the workflow is still
        /// available, and the claimed workflows are loaded in one request.
        /// </summary>
        public override async Task<IEnumerable<Workflow>> ClaimNextAsync(string nodeId, int count)
        {
            var result = await TryClaimAsync(nodeId, count, waitForNonStaleResults: false);
            if (result.Workflows.Count == 0 && result.Retry)
            {
                // the index was behind (all the candidates were already claimed, or recently released workflows were
                // missing), so try again against an up-to-date index
                result = await TryClaimAsync(nodeId, count, waitForNonStaleResults: true);
            }
            return result.Workflows;
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.  Optimistic
        /// concurrency ensures the workflow isn't claimed by another node between the check and the save.
        /// </summary>
        public override bool SaveClaimed(Workflow workflow, string nodeId)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
            for (int attempt = 1; ; attempt++)
            {
                using (IDocumentSession session = this.OpenSession())
                {
                    session.Advanced.UseOptimisticConcurrency = true;
                    RavenWorkflow stored = session.Load<RavenWorkflow>(fid);
                    if (!IsClaimable(stored, nodeId))
                    {
                        return false;
                    }
                    stored.Workflow = workflow;
                    stored.WorkflowType = workflow.GetType().AssemblyQualifiedName;
                    try
                    {
                        session.SaveChanges();
                        return true;
                    }
                    catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                    {
                        // the document changed after it was loaded - check the lock again
                    }
                }
            }
        }

        /// <summary>
        /// Saves a workflow claimed by a node, if it is still locked by the node or not locked at all.  Optimistic
        /// concurrency ensures the workflow isn't claimed by another node between the check and the save.
        /// </summary>
        public override async Task<bool> SaveClaimedAsync(Workflow workflow, string nodeId)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
            for (int attempt = 1; ; attempt++)
            {
                using (var session = this.OpenAsyncSession())
                {
                    session.Advanced.UseOptimisticConcurrency = true;
                    RavenWorkflow stored = await session.LoadAsync<RavenWorkflow>(fid);
                    if (!IsClaimable(stored, nodeId))
                    {
                        return false;
                    }
                    stored.Workflow = workflow;
                    stored.WorkflowType = workflow.GetType().AssemblyQualifiedName;
                    try
                    {
                        await session.SaveChangesAsync();
                        return true;
                    }
                    catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                    {
                        // the document changed after it was loaded - check the lock again
                    }
                }
            }
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.  The delete and
        /// the insert into the completed collection are saved in a single transaction.
        /// </summary>
        public override bool ArchiveClaimed(Workflow workflow, string nodeId)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
            for (int attempt = 1; ; attempt++)
            {
                using (IDocumentSession session = this.OpenSession())
                {
                    session.Advanced.UseOptimisticConcurrency = true;
                    RavenWorkflow stored = session.Load<RavenWorkflow>(fid);
                    if (!IsClaimable(stored, nodeId))
                    {
                        return false;
                    }
                    session.Delete(stored);
                    session.Store(new RavenCompletedWorkflow(workflow));
                    try
                    {
                        session.SaveChanges();
                        return true;
                    }
                    catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                    {
                        // the document changed after it was loaded - check the lock again
                    }
                }
            }
        }

        /// <summary>
        /// Archives a workflow claimed by a node, if it is still locked by the node or not locked at all.  The delete and
        /// the insert into the completed collection are saved in a single transaction.
        /// </summary>
        public override async Task<bool> ArchiveClaimedAsync(Workflow workflow, string nodeId)
        {
            string fid = RavenDbIdUtility.FormatWorkflowId(workflow.Id);
            for (int attempt = 1; ; attempt++)
            {
                using (var session = this.OpenAsyncSession())
                {
                    session.Advanced.UseOptimisticConcurrency = true;
                    RavenWorkflow stored = await session.LoadAsync<RavenWorkflow>(fid);
                    if (!IsClaimable(stored, nodeId))
                    {
                        return false;
                    }
                    session.Delete(stored);
                    await session.StoreAsync(new RavenCompletedWorkflow(workflow));
                    try
                    {
                        await session.SaveChangesAsync();
                        return true;
                    }
                    catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
                    {
                        // the document changed after it was loaded - check the lock again
                    }
                }
            }
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override void ReleaseLocks(string nodeId)
        {
            Operation operation = _documentStore.Operations.Send(ReleaseLocksOperation(nodeId, null, null));
            operation.WaitForCompletion(OperationTimeout);
        }

        /// <summary>
        /// Releases all workflows locked by a node.
        /// </summary>
        public override async Task ReleaseLocksAsync(string nodeId)
        {
            Operation operation = await _documentStore.Operations.SendAsync(ReleaseLocksOperation(nodeId, null, null));
            await operation.WaitForCompletionAsync(OperationTimeout);
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override void ReleaseStaleLocks(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            Operation operation = _documentStore.Operations.Send(ReleaseLocksOperation(nodeId, lockedBefore, excludeIds));
            operation.WaitForCompletion(OperationTimeout);
        }

        /// <summary>
        /// Releases workflows locked by a node before <c>lockedBefore</c>, excluding the supplied workflow ids.
        /// </summary>
        public override async Task ReleaseStaleLocksAsync(string nodeId, DateTime lockedBefore, IEnumerable<Guid> excludeIds)
        {
            Operation operation = await _documentStore.Operations.SendAsync(ReleaseLocksOperation(nodeId, lockedBefore, excludeIds));
            await operation.WaitForCompletionAsync(OperationTimeout);
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.  This waits for the index to be up to date.
        /// </summary>
        public override IEnumerable<string> GetLockOwners()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<RavenWorkflow>()
                    .Customize(x => x.WaitForNonStaleResults(OperationTimeout))
                    .Where(x => x.Workflow.LockedBy != null)
                    .Select(x => x.Workflow.LockedBy)
                    .Distinct()
                    .ToList()
                    .Where(x => x != null)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the distinct ids of the nodes that currently hold locks on workflows.  This waits for the index to be up to date.
        /// </summary>
        public override async Task<IEnumerable<string>> GetLockOwnersAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                List<string> owners = await session.Query<RavenWorkflow>()
                    .Customize(x => x.WaitForNonStaleResults(OperationTimeout))
                    .Where(x => x.Workflow.LockedBy != null)
                    .Select(x => x.Workflow.LockedBy)
                    .Distinct()
                    .ToListAsync();

                // documents saved before multi-node support have no LockedBy field, which RavenDB treats as not null
                return owners.Where(x => x != null).ToList();
            }
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.  The session waits for indexes,
        /// so the node is visible to GetNodes before the node claims any workflows.
        /// </summary>
        public override void RegisterNode(WorkflowNode node)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                node.LastHeartbeat = DateTime.UtcNow;
                session.Store(node, node.Id);
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Registers a workflow server node, setting its heartbeat to the current time.  The session waits for indexes,
        /// so the node is visible to GetNodes before the node claims any workflows.
        /// </summary>
        public override async Task RegisterNodeAsync(WorkflowNode node)
        {
            using (var session = this.OpenAsyncSession())
            {
                node.LastHeartbeat = DateTime.UtcNow;
                await session.StoreAsync(node, node.Id);
                await session.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Updates the heartbeat of a node, returning false if the node no longer exists.
        /// </summary>
        public override bool Heartbeat(string nodeId)
        {
            PatchStatus status = _documentStore.Operations.Send(HeartbeatOperation(nodeId));
            return status != PatchStatus.DocumentDoesNotExist;
        }

        /// <summary>
        /// Updates the heartbeat of a node, returning false if the node no longer exists.
        /// </summary>
        public override async Task<bool> HeartbeatAsync(string nodeId)
        {
            PatchStatus status = await _documentStore.Operations.SendAsync(HeartbeatOperation(nodeId));
            return status != PatchStatus.DocumentDoesNotExist;
        }

        /// <summary>
        /// Gets all registered nodes.  This waits for the index to be up to date, as a node missing from the results
        /// would have its locks released.
        /// </summary>
        public override IEnumerable<WorkflowNode> GetNodes()
        {
            using (IDocumentSession session = this.OpenSession())
            {
                return session.Query<WorkflowNode>()
                    .Customize(x => x.WaitForNonStaleResults(OperationTimeout))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all registered nodes.  This waits for the index to be up to date, as a node missing from the results
        /// would have its locks released.
        /// </summary>
        public override async Task<IEnumerable<WorkflowNode>> GetNodesAsync()
        {
            using (var session = this.OpenAsyncSession())
            {
                return await session.Query<WorkflowNode>()
                    .Customize(x => x.WaitForNonStaleResults(OperationTimeout))
                    .ToListAsync();
            }
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override void RemoveNode(string nodeId)
        {
            using (IDocumentSession session = this.OpenSession())
            {
                session.Delete(nodeId);
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Removes a node.
        /// </summary>
        public override async Task RemoveNodeAsync(string nodeId)
        {
            using (var session = this.OpenAsyncSession())
            {
                session.Delete(nodeId);
                await session.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.  Heartbeats are set from the clocks of
        /// the workflow servers, so these need to be kept in sync.
        /// </summary>
        public override long RemoveStaleNodes(TimeSpan timeout)
        {
            Operation operation = _documentStore.Operations.Send(RemoveStaleNodesOperation(timeout));
            return operation.WaitForCompletion<BulkOperationResult>(OperationTimeout).Total;
        }

        /// <summary>
        /// Removes all nodes that have not sent a heartbeat within the timeout.  Heartbeats are set from the clocks of
        /// the workflow servers, so these need to be kept in sync.
        /// </summary>
        public override async Task<long> RemoveStaleNodesAsync(TimeSpan timeout)
        {
            Operation operation = await _documentStore.Operations.SendAsync(RemoveStaleNodesOperation(timeout));
            BulkOperationResult result = await operation.WaitForCompletionAsync<BulkOperationResult>(OperationTimeout);
            return result.Total;
        }

        private static bool IsClaimable(RavenWorkflow stored, string nodeId)
        {
            return stored != null && (stored.Workflow.LockedBy == null || stored.Workflow.LockedBy == nodeId);
        }

        /// <summary>
        /// Tries to claim up to <c>count</c> of the top candidate workflows.  <c>retry</c> is set if nothing was claimed
        /// but an up-to-date index might return a different result.
        /// </summary>
        private List<Workflow> TryClaim(string nodeId, int count, bool waitForNonStaleResults, out bool retry)
        {
            DateTime now = DateTime.UtcNow;
            List<string> candidateIds;
            QueryStatistics stats;
            using (IDocumentSession session = this.OpenSession())
            {
                var query = session.Advanced.RawQuery<ClaimCandidate>(ClaimCandidatesQuery(count))
                    .AddParameter("now", now)
                    .Statistics(out stats);
                if (waitForNonStaleResults)
                {
                    query = query.WaitForNonStaleResults(TimeSpan.FromSeconds(5));
                }
                candidateIds = query.ToList().Select(x => x.Id).ToList();
            }
            retry = candidateIds.Count > 0 || stats.IsStale;

            List<string> claimedIds = new List<string>();
            foreach (string id in candidateIds)
            {
                if (claimedIds.Count == count)
                {
                    break;
                }
                PatchStatus status = _documentStore.Operations.Send(ClaimOperation(id, nodeId, now));
                if (status == PatchStatus.Patched)
                {
                    claimedIds.Add(id);
                }
            }
            if (claimedIds.Count == 0)
            {
                return new List<Workflow>();
            }

            using (IDocumentSession session = this.OpenSession())
            {
                Dictionary<string, RavenWorkflow> claimed = session.Load<RavenWorkflow>(claimedIds);
                return claimedIds.Select(id => claimed[id]?.Workflow).Where(x => x != null).ToList();
            }
        }

        /// <summary>
        /// Tries to claim up to <c>count</c> of the top candidate workflows.  <c>Retry</c> is set if nothing was claimed
        /// but an up-to-date index might return a different result.
        /// </summary>
        private async Task<(List<Workflow> Workflows, bool Retry)> TryClaimAsync(string nodeId, int count, bool waitForNonStaleResults)
        {
            DateTime now = DateTime.UtcNow;
            List<string> candidateIds;
            QueryStatistics stats;
            using (var session = this.OpenAsyncSession())
            {
                var query = session.Advanced.AsyncRawQuery<ClaimCandidate>(ClaimCandidatesQuery(count))
                    .AddParameter("now", now)
                    .Statistics(out stats);
                if (waitForNonStaleResults)
                {
                    query = query.WaitForNonStaleResults(TimeSpan.FromSeconds(5));
                }
                candidateIds = (await query.ToListAsync()).Select(x => x.Id).ToList();
            }
            bool retry = candidateIds.Count > 0 || stats.IsStale;

            List<string> claimedIds = new List<string>();
            foreach (string id in candidateIds)
            {
                if (claimedIds.Count == count)
                {
                    break;
                }
                PatchStatus status = await _documentStore.Operations.SendAsync(ClaimOperation(id, nodeId, now));
                if (status == PatchStatus.Patched)
                {
                    claimedIds.Add(id);
                }
            }
            if (claimedIds.Count == 0)
            {
                return (new List<Workflow>(), retry);
            }

            using (var session = this.OpenAsyncSession())
            {
                Dictionary<string, RavenWorkflow> claimed = await session.LoadAsync<RavenWorkflow>(claimedIds);
                return (claimedIds.Select(id => claimed[id]?.Workflow).Where(x => x != null).ToList(), retry);
            }
        }

        /// <summary>
        /// Gets the RQL for the top unlocked workflows that are due.  Documents saved before multi-node support have no
        /// LockedBy field at all, which RavenDB does not treat as null, so these are matched with "not exists".  This is
        /// written in RQL as the LINQ provider has no way of expressing it.  Extra candidates are read in case other
        /// nodes claim some of them first.
        /// </summary>
        private string ClaimCandidatesQuery(int count)
        {
            return $@"from '{WorkflowCollection}'
where Workflow.IsSuspended = false and Workflow.ResumeOn <= $now and (Workflow.LockedBy = null or (true and not exists(Workflow.LockedBy)))
order by Workflow.Priority as long desc, Workflow.RetryCount as long desc, Workflow.CreatedOn
select id() as Id
limit {count + ClaimCandidateCount}";
        }

        private class ClaimCandidate
        {
            public string Id { get; set; }
        }

        private static PatchOperation ClaimOperation(string id, string nodeId, DateTime now)
        {
            return new PatchOperation(id, null, new PatchRequest
            {
                Script = ClaimScript,
                Values = new Dictionary<string, object> { { "node", nodeId }, { "now", now } }
            });
        }

        private static PatchOperation HeartbeatOperation(string nodeId)
        {
            return new PatchOperation(nodeId, null, new PatchRequest
            {
                Script = HeartbeatScript,
                Values = new Dictionary<string, object> { { "now", DateTime.UtcNow } }
            });
        }

        /// <summary>
        /// Creates a patch-by-query operation that releases a node's locks.  If <c>lockedBefore</c> is supplied, only locks
        /// taken before that time are released, and workflows in <c>excludeIds</c> are skipped.
        /// </summary>
        private PatchByQueryOperation ReleaseLocksOperation(string nodeId, DateTime? lockedBefore, IEnumerable<Guid> excludeIds)
        {
            string condition = "this.Workflow.LockedBy === $node";
            Parameters parameters = new Parameters { { "node", nodeId } };
            if (lockedBefore.HasValue)
            {
                condition += " && this.Workflow.LockedOn < $before && $exclude.indexOf(id(this)) < 0";
                parameters.Add("before", lockedBefore.Value);
                parameters.Add("exclude", (excludeIds ?? Enumerable.Empty<Guid>()).Select(RavenDbIdUtility.FormatWorkflowId).ToArray());
            }

            IndexQuery query = new IndexQuery
            {
                Query = $"from '{WorkflowCollection}' where Workflow.LockedBy = $node update {{ if ({condition}) {{ this.Workflow.LockedBy = null; this.Workflow.LockedOn = null; }} }}",
                QueryParameters = parameters
            };
            return new PatchByQueryOperation(query, new QueryOperationOptions { AllowStale = false, StaleTimeout = OperationTimeout });
        }

        private DeleteByQueryOperation RemoveStaleNodesOperation(TimeSpan timeout)
        {
            IndexQuery query = new IndexQuery
            {
                Query = $"from '{NodeCollection}' where LastHeartbeat < $cutoff",
                QueryParameters = new Parameters { { "cutoff", DateTime.UtcNow.Subtract(timeout) } }
            };
            return new DeleteByQueryOperation(query, new QueryOperationOptions { AllowStale = false, StaleTimeout = OperationTimeout });
        }


    }
}
