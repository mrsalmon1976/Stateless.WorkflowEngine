using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Stateless.WorkflowEngine.WebConsole.Features.Workflows
{
    public class WorkflowIdsRequest
    {
        public Guid[] WorkflowIds { get; set; } = Array.Empty<Guid>();
    }

    public class WorkflowResponse
    {
        public string? WorkflowJson { get; set; }

        public bool IsSuspended { get; set; }

        /// <summary>
        /// Gets/sets the summary of the workflow, as returned in the workflow list.
        /// </summary>
        public UIWorkflow? Workflow { get; set; }
    }

    [Route("api/connections/{connectionId:guid}")]
    public class WorkflowsController : AuthenticatedBaseController
    {
        public const int DefaultCount = 50;
        // the list is filtered and sorted in the browser, so keep it to a size the browser can render comfortably
        public const int MaxCount = 1000;

        private readonly IUserStore _userStore;
        private readonly IWorkflowStoreFactory _workflowStoreFactory;
        private readonly IWorkflowInfoService _workflowInfoService;

        public WorkflowsController(IUserStore userStore, IWorkflowStoreFactory workflowStoreFactory, IWorkflowInfoService workflowInfoService)
        {
            _userStore = userStore;
            _workflowStoreFactory = workflowStoreFactory;
            _workflowInfoService = workflowInfoService;
        }

        [HttpGet("workflows")]
        public async Task<IActionResult> List(Guid connectionId, [FromQuery] int count = DefaultCount)
        {
            if (count <= 0 || count > MaxCount) return ValidationFailed($"Count must be between 1 and {MaxCount}");

            ConnectionModel? connection = _userStore.GetConnection(connectionId);
            if (connection == null) return ConnectionNotFound();

            return Ok(await _workflowInfoService.GetIncompleteWorkflowsAsync(connection, count));
        }

        [HttpGet("workflows/{workflowId:guid}")]
        public async Task<IActionResult> Get(Guid connectionId, Guid workflowId)
        {
            ConnectionModel? connection = _userStore.GetConnection(connectionId);
            if (connection == null) return ConnectionNotFound();

            IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(connection);
            string? json = await store.GetWorkflowAsJsonAsync(workflowId);
            if (json == null)
            {
                return NotFound(new { error = "No workflow found matching the supplied workflow and connection (the workflow may have completed)." });
            }

            UIWorkflow wf = _workflowInfoService.GetWorkflowInfoFromJson(connection.WorkflowStoreType, json);
            if (!String.IsNullOrEmpty(wf.QualifiedName))
            {
                wf.WorkflowGraph = (await store.GetDefinitionByQualifiedNameAsync(wf.QualifiedName))?.Graph;
            }
            return Ok(new WorkflowResponse { WorkflowJson = json, IsSuspended = wf.IsSuspended, Workflow = wf });
        }

        [HttpPost("workflows/suspend")]
        [Authorize(Policy = Claims.SuspendWorkflow)]
        public Task<IActionResult> Suspend(Guid connectionId, [FromBody] WorkflowIdsRequest request)
        {
            return ForEachWorkflowAsync(connectionId, request, (store, id) => store.SuspendWorkflowAsync(id));
        }

        [HttpPost("workflows/unsuspend")]
        [Authorize(Policy = Claims.UnsuspendWorkflow)]
        public Task<IActionResult> Unsuspend(Guid connectionId, [FromBody] WorkflowIdsRequest request)
        {
            return ForEachWorkflowAsync(connectionId, request, (store, id) => store.UnsuspendWorkflowAsync(id));
        }

        [HttpPost("workflows/delete")]
        [Authorize(Policy = Claims.RemoveWorkflow)]
        public Task<IActionResult> Delete(Guid connectionId, [FromBody] WorkflowIdsRequest request)
        {
            return ForEachWorkflowAsync(connectionId, request, (store, id) => store.DeleteAsync(id));
        }

        /// <summary>
        /// Marks workflows as complete, moving them into the completed collection without running any further steps.
        /// </summary>
        [HttpPost("workflows/complete")]
        [Authorize(Policy = Claims.CompleteWorkflow)]
        public Task<IActionResult> Complete(Guid connectionId, [FromBody] WorkflowIdsRequest request)
        {
            return ForEachWorkflowAsync(connectionId, request, (store, id) => store.CompleteWorkflowAsync(id));
        }

        [HttpGet("definitions")]
        public async Task<IActionResult> Definition(Guid connectionId, [FromQuery] string? qualifiedName)
        {
            if (String.IsNullOrWhiteSpace(qualifiedName)) return ValidationFailed("Qualified name is required");

            ConnectionModel? connection = _userStore.GetConnection(connectionId);
            if (connection == null) return ConnectionNotFound();

            IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(connection);
            WorkflowDefinition? definition = await store.GetDefinitionByQualifiedNameAsync(qualifiedName);
            if (definition == null)
            {
                return NotFound(new { error = $"No workflow definition found for '{qualifiedName}'" });
            }
            return Ok(definition);
        }

        private async Task<IActionResult> ForEachWorkflowAsync(Guid connectionId, WorkflowIdsRequest request, Func<IWorkflowStore, Guid, Task> action)
        {
            if (request.WorkflowIds.Length == 0) return ValidationFailed("No workflows selected");

            ConnectionModel? connection = _userStore.GetConnection(connectionId);
            if (connection == null) return ConnectionNotFound();

            IWorkflowStore store = _workflowStoreFactory.GetWorkflowStore(connection);
            foreach (Guid workflowId in request.WorkflowIds)
            {
                await action(store, workflowId);
            }
            return NoContent();
        }

        private IActionResult ConnectionNotFound()
        {
            return NotFound(new { error = "Connection not found" });
        }
    }
}
