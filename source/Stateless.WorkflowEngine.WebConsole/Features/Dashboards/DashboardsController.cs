using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Stateless.WorkflowEngine.WebConsole.Features.Connections;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Stateless.WorkflowEngine.WebConsole.Features.Dashboards
{
    [Route("api/dashboards")]
    public class DashboardsController : AuthenticatedBaseController
    {
        private readonly IUserStore _userStore;
        private readonly DashboardValidator _validator;
        private readonly IWorkflowStoreFactory _workflowStoreFactory;
        private readonly IMemoryCache _memoryCache;

        public DashboardsController(IUserStore userStore, DashboardValidator validator, IWorkflowStoreFactory workflowStoreFactory, IMemoryCache memoryCache)
        {
            _userStore = userStore;
            _validator = validator;
            _workflowStoreFactory = workflowStoreFactory;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Lists the dashboards - the default dashboard first, then the others by name.
        /// </summary>
        [HttpGet]
        public IActionResult List()
        {
            var connectionCounts = _userStore.Connections
                .GroupBy(x => x.DashboardId)
                .ToDictionary(x => x.Key ?? Guid.Empty, x => x.Count());
            var dashboards = _userStore.Dashboards
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => DashboardResponse.FromModel(x, connectionCounts.GetValueOrDefault(x.Id)));
            return Ok(dashboards);
        }

        [HttpGet("{id:guid}")]
        public IActionResult Get(Guid id)
        {
            DashboardModel? dashboard = _userStore.GetDashboard(id);
            if (dashboard == null) return DashboardNotFound();
            return Ok(ToResponse(dashboard));
        }

        [HttpPost]
        [Authorize(Policy = Claims.DashboardAdd)]
        public IActionResult Add([FromBody] DashboardRequest request)
        {
            var errors = _validator.Validate(request, null);
            if (errors.Count > 0) return ValidationFailed(errors);

            var dashboard = new DashboardModel { Name = request.Name!.Trim() };
            _userStore.SaveDashboard(dashboard);
            return Ok(ToResponse(dashboard));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = Claims.DashboardEdit)]
        public IActionResult Update(Guid id, [FromBody] DashboardRequest request)
        {
            DashboardModel? existing = _userStore.GetDashboard(id);
            if (existing == null) return DashboardNotFound();

            var errors = _validator.Validate(request, id);
            if (errors.Count > 0) return ValidationFailed(errors);

            var dashboard = new DashboardModel { Id = existing.Id, Name = request.Name!.Trim(), IsDefault = existing.IsDefault };
            _userStore.SaveDashboard(dashboard);
            return Ok(ToResponse(dashboard));
        }

        /// <summary>
        /// Deletes a dashboard and the connections on it - workflows in the stores are not affected.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = Claims.DashboardDelete)]
        public IActionResult Delete(Guid id)
        {
            DashboardModel? existing = _userStore.GetDashboard(id);
            if (existing == null) return DashboardNotFound();

            if (existing.IsDefault)
            {
                return ValidationFailed("The default dashboard cannot be deleted");
            }

            var connectionIds = _userStore.Connections.Where(x => x.DashboardId == id).Select(x => x.Id).ToList();
            _userStore.DeleteDashboard(id);
            foreach (Guid connectionId in connectionIds)
            {
                _workflowStoreFactory.Evict(connectionId);
                _memoryCache.Remove(ConnectionsController.InfoCacheKey(connectionId));
            }
            return NoContent();
        }

        private DashboardResponse ToResponse(DashboardModel dashboard)
        {
            return DashboardResponse.FromModel(dashboard, _userStore.Connections.Count(x => x.DashboardId == dashboard.Id));
        }

        private IActionResult DashboardNotFound()
        {
            return NotFound(new { error = "Dashboard not found" });
        }
    }
}
