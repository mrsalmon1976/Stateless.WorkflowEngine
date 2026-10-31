using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Stateless.WorkflowEngine.WebConsole.Features.Connections
{
    [Route("api/connections")]
    public class ConnectionsController : AuthenticatedBaseController
    {
        public static readonly TimeSpan InfoCacheDuration = TimeSpan.FromSeconds(5);

        private readonly IUserStore _userStore;
        private readonly ConnectionValidator _validator;
        private readonly IConnectionPasswordProtector _passwordProtector;
        private readonly IWorkflowStoreFactory _workflowStoreFactory;
        private readonly IWorkflowInfoService _workflowInfoService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ConnectionsController> _logger;

        public ConnectionsController(IUserStore userStore, ConnectionValidator validator, IConnectionPasswordProtector passwordProtector,
            IWorkflowStoreFactory workflowStoreFactory, IWorkflowInfoService workflowInfoService, IMemoryCache memoryCache, ILogger<ConnectionsController> logger)
        {
            _userStore = userStore;
            _validator = validator;
            _passwordProtector = passwordProtector;
            _workflowStoreFactory = workflowStoreFactory;
            _workflowInfoService = workflowInfoService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public static string InfoCacheKey(Guid connectionId) => $"ConnectionInfo_{connectionId}";

        [HttpGet]
        public IActionResult List()
        {
            var connections = _userStore.Connections
                .OrderBy(x => x.Host)
                .ThenBy(x => x.Database)
                .Select(ConnectionResponse.FromModel);
            return Ok(connections);
        }

        [HttpGet("{id:guid}")]
        public IActionResult Get(Guid id)
        {
            ConnectionModel? connection = _userStore.GetConnection(id);
            if (connection == null) return ConnectionNotFound();
            return Ok(ConnectionResponse.FromModel(connection));
        }

        [HttpPost]
        [Authorize(Policy = Claims.ConnectionAdd)]
        public IActionResult Add([FromBody] ConnectionRequest request)
        {
            var errors = _validator.Validate(request);
            if (errors.Count > 0) return ValidationFailed(errors);

            ConnectionModel connection = ToModel(request, null);
            _userStore.SaveConnection(connection);
            return Ok(ConnectionResponse.FromModel(connection));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = Claims.ConnectionEdit)]
        public IActionResult Update(Guid id, [FromBody] ConnectionRequest request)
        {
            ConnectionModel? existing = _userStore.GetConnection(id);
            if (existing == null) return ConnectionNotFound();

            var errors = _validator.Validate(request);
            if (errors.Count > 0) return ValidationFailed(errors);

            ConnectionModel connection = ToModel(request, existing);
            _userStore.SaveConnection(connection);
            _workflowStoreFactory.Evict(id);
            _memoryCache.Remove(InfoCacheKey(id));
            return Ok(ConnectionResponse.FromModel(connection));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = Claims.ConnectionDelete)]
        public IActionResult Delete(Guid id)
        {
            if (_userStore.GetConnection(id) == null) return ConnectionNotFound();

            _userStore.DeleteConnection(id);
            _workflowStoreFactory.Evict(id);
            _memoryCache.Remove(InfoCacheKey(id));
            return NoContent();
        }

        [HttpGet("{id:guid}/info")]
        public IActionResult Info(Guid id)
        {
            if (_memoryCache.TryGetValue(InfoCacheKey(id), out WorkflowStoreInfo? cached) && cached != null)
            {
                return Ok(cached);
            }

            ConnectionModel? connection = _userStore.GetConnection(id);
            if (connection == null) return ConnectionNotFound();

            WorkflowStoreInfo info = _workflowInfoService.GetWorkflowStoreInfo(connection);
            _memoryCache.Set(InfoCacheKey(id), info, InfoCacheDuration);
            return Ok(info);
        }

        [HttpPost("test")]
        [Authorize(Policy = WebConsoleServiceCollectionExtensions.ConnectionTestPolicy)]
        public IActionResult Test([FromBody] ConnectionRequest request)
        {
            var errors = _validator.Validate(request);
            if (errors.Count > 0) return ValidationFailed(errors);

            ConnectionModel? existing = request.Id.HasValue ? _userStore.GetConnection(request.Id.Value) : null;
            ConnectionModel connection = ToModel(request, existing);

            // use a throwaway id so the test doesn't pollute the client cache for the saved connection
            connection.Id = Guid.NewGuid();
            try
            {
                var store = _workflowStoreFactory.GetWorkflowStore(connection);
                store.GetIncompleteCount();
                return Ok(new ConnectionTestResponse { Success = true, Message = "Connection succeeded" });
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Connection test to {Host} failed", request.Host);
                string message = ex.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ? "Authentication failure" : ex.Message;
                return Ok(new ConnectionTestResponse { Success = false, Message = $"Connection failed: {message}" });
            }
            finally
            {
                _workflowStoreFactory.Evict(connection.Id);
            }
        }

        /// <summary>
        /// Creates a connection model from a request.  If no password is supplied for an existing connection,
        /// the stored password is retained (unless the user has been cleared).
        /// </summary>
        private ConnectionModel ToModel(ConnectionRequest request, ConnectionModel? existing)
        {
            var model = new ConnectionModel
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                WorkflowStoreType = request.WorkflowStoreType,
                Host = request.Host?.Trim(),
                Port = request.Port,
                Database = request.Database?.Trim(),
                User = String.IsNullOrWhiteSpace(request.User) ? null : request.User.Trim(),
                ReplicaSet = String.IsNullOrWhiteSpace(request.ReplicaSet) ? null : request.ReplicaSet.Trim(),
                ActiveCollection = request.ActiveCollection?.Trim(),
                CompletedCollection = request.CompletedCollection?.Trim(),
                WorkflowDefinitionCollection = String.IsNullOrWhiteSpace(request.WorkflowDefinitionCollection) ? null : request.WorkflowDefinitionCollection.Trim()
            };

            if (!String.IsNullOrEmpty(request.Password))
            {
                model.Password = _passwordProtector.Protect(request.Password);
            }
            else if (existing != null && model.User != null)
            {
                model.Password = existing.Password;
                model.Key = existing.Key;
            }

            return model;
        }

        private IActionResult ConnectionNotFound()
        {
            return NotFound(new { error = "Connection not found" });
        }
    }
}
