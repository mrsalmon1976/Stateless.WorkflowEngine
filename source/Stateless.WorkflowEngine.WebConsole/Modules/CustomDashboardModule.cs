using AutoMapper;
using Nancy;
using Nancy.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Caching;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.CustomDashboard;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class CustomDashboardModule : WebConsoleSecureModule
    {
        private readonly IUserStore _userStore;
        private readonly IWorkflowInfoService _workflowInfoService;
        private readonly ICacheProvider _cacheProvider;
        private readonly IMapper _mapper;

        public CustomDashboardModule(IUserStore userStore, IWorkflowInfoService workflowInfoService, ICacheProvider cacheProvider, IMapper mapper) : base()
        {
            _userStore = userStore;
            _workflowInfoService = workflowInfoService;
            _cacheProvider = cacheProvider;
            _mapper = mapper;

            Get[Actions.CustomDashboard.Default] = (x) =>
            {
                AddScript(Scripts.CustomDashboardView);
                return Default();
            };

            Get[Actions.CustomDashboard.List] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.CustomDashboardAdd });
                return List();
            };

            Get[Actions.CustomDashboard.Connections] = (x) =>
            {
                return Connections();
            };

            Post[Actions.CustomDashboard.WorkflowTypes] = (x) =>
            {
                return WorkflowTypes();
            };

            Post[Actions.CustomDashboard.Info] = (x) =>
            {
                return Info();
            };

            Post[Actions.CustomDashboard.Save] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.CustomDashboardAdd });
                return Save();
            };

            Post[Actions.CustomDashboard.Delete] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.CustomDashboardDelete });
                return Delete();
            };

            Post[Actions.CustomDashboard.RemoveConnection] = (x) =>
            {
                this.RequiresClaims(new[] { Claims.CustomDashboardAdd });
                return RemoveConnection();
            };
        }

        public dynamic Connections()
        {
            Guid id;
            if (!Guid.TryParse(Request.Query["id"], out id))
            {
                return Response.AsJson(new BasicResult(false, "Invalid dashboard id"), HttpStatusCode.BadRequest);
            }

            var dashboard = _userStore.GetCustomDashboard(id);
            if (dashboard == null)
            {
                return Response.AsJson(new BasicResult(false, "Dashboard not found"), HttpStatusCode.NotFound);
            }

            var allConnections = _mapper.Map<List<ConnectionModel>, List<ConnectionViewModel>>(_userStore.Connections)
                                         .OrderBy(x => x.Host).ThenBy(x => x.Database).ToList();
            var filteredConnections = dashboard.ConnectionIds.Any()
                ? allConnections.Where(c => dashboard.ConnectionIds.Contains(c.Id.ToString())).ToList()
                : allConnections;
            var model = new CustomDashboardConnectionsViewModel
            {
                Dashboard = dashboard,
                Connections = filteredConnections,
                CurrentUserCanDeleteConnection = this.Context.CurrentUser.HasClaim(Claims.CustomDashboardAdd)
            };
            return this.View[Views.CustomDashboard.ConnectionsPartial, model];
        }

        public dynamic RemoveConnection()
        {
            Guid dashboardId = Guid.Empty;
            Guid connectionId = Guid.Empty;
            if (!Guid.TryParse(Request.Form["dashboardId"], out dashboardId) ||
                !Guid.TryParse(Request.Form["connectionId"], out connectionId))
            {
                return Response.AsJson(new BasicResult(false, "Invalid ids"), HttpStatusCode.BadRequest);
            }

            var dashboard = _userStore.GetCustomDashboard(dashboardId);
            if (dashboard == null)
            {
                return Response.AsJson(new BasicResult(false, "Dashboard not found"), HttpStatusCode.NotFound);
            }

            dashboard.ConnectionIds.RemoveAll(x => String.Equals(x, connectionId.ToString(), StringComparison.OrdinalIgnoreCase));
            _userStore.Save();
            return Response.AsJson(new BasicResult(true));
        }

        public dynamic Default()
        {
            var model = new CustomDashboardViewModel
            {
                Connections = _mapper.Map<List<ConnectionModel>, List<ConnectionViewModel>>(_userStore.Connections)
                                     .OrderBy(x => x.Host).ThenBy(x => x.Database).ToList(),
                CurrentUserCanAdd = this.Context.CurrentUser.HasClaim(Claims.CustomDashboardAdd),
                CurrentUserCanDelete = this.Context.CurrentUser.HasClaim(Claims.CustomDashboardDelete)
            };
            return this.View[Views.CustomDashboard.Default, model];
        }

        public dynamic Delete()
        {
            Guid id;
            if (!Guid.TryParse(Request.Form["id"], out id))
            {
                return Response.AsJson(new BasicResult(false, "Invalid dashboard id"), HttpStatusCode.BadRequest);
            }

            var dashboard = _userStore.GetCustomDashboard(id);
            if (dashboard == null)
            {
                return Response.AsJson(new BasicResult(false, "Dashboard not found"), HttpStatusCode.NotFound);
            }

            _userStore.CustomDashboards.Remove(dashboard);
            _userStore.Save();
            return Response.AsJson(new BasicResult(true));
        }

        public dynamic Info()
        {
            Guid dashboardId;
            Guid connectionId = Guid.Empty;
            if (!Guid.TryParse(Request.Form["dashboardId"], out dashboardId) ||
                !Guid.TryParse(Request.Form["connectionId"], out connectionId))
            {
                return Response.AsJson(new BasicResult(false, "Invalid ids"), HttpStatusCode.BadRequest);
            }

            string cacheKey = CacheKeys.CustomDashboardInfo(dashboardId.ToString(), connectionId.ToString());
            var cached = _cacheProvider.Get<CustomDashboardInfoViewModel>(cacheKey);
            if (cached != null)
            {
                return Response.AsJson(cached);
            }

            var dashboard = _userStore.GetCustomDashboard(dashboardId);
            var conn = _userStore.GetConnection(connectionId);
            if (dashboard == null || conn == null)
            {
                return Response.AsJson(new BasicResult(false, "Not found"), HttpStatusCode.NotFound);
            }

            var info = _workflowInfoService.GetCustomDashboardInfo(dashboard, conn);
            _cacheProvider.Set(cacheKey, info, TimeSpan.FromSeconds(5));
            return Response.AsJson(info);
        }

        public dynamic List()
        {
            var model = new CustomDashboardListViewModel
            {
                CurrentUserCanDelete = this.Context.CurrentUser.HasClaim(Claims.CustomDashboardDelete),
                CurrentUserCanAdd = this.Context.CurrentUser.HasClaim(Claims.CustomDashboardAdd)
            };
            model.Dashboards.AddRange(_userStore.CustomDashboards.OrderBy(x => x.Name));
            return this.View[Views.CustomDashboard.ListPartial, model];
        }

        public dynamic Save()
        {
            string name = Request.Form["name"];
            if (string.IsNullOrWhiteSpace(name))
            {
                return Response.AsJson(new BasicResult(false, "Dashboard name is required"));
            }

            var connectionIdsRaw = (string)Request.Form["connectionIds"];
            var connectionIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(connectionIdsRaw))
            {
                connectionIds.AddRange(
                    connectionIdsRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(x => x.Trim())
                                     .Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            Guid id;
            if (Guid.TryParse((string)Request.Form["id"], out id) && id != Guid.Empty)
            {
                var existing = _userStore.GetCustomDashboard(id);
                if (existing != null)
                {
                    existing.Name = name;
                    existing.ConnectionIds = connectionIds;
                    _userStore.Save();
                    return Response.AsJson(new BasicResult(true));
                }
            }

            var dashboard = new CustomDashboardModel
            {
                Name = name,
                ConnectionIds = connectionIds
            };
            _userStore.CustomDashboards.Add(dashboard);
            _userStore.Save();
            return Response.AsJson(new BasicResult(true));
        }

        public dynamic WorkflowTypes()
        {
            Guid connectionId;
            if (!Guid.TryParse(Request.Form["connectionId"], out connectionId))
            {
                return Response.AsJson(new BasicResult(false, "Invalid connection id"), HttpStatusCode.BadRequest);
            }

            var conn = _userStore.GetConnection(connectionId);
            if (conn == null)
            {
                return Response.AsJson(new BasicResult(false, "Connection not found"), HttpStatusCode.NotFound);
            }

            try
            {
                var types = _workflowInfoService.GetWorkflowTypes(conn);
                return Response.AsJson(types);
            }
            catch (Exception ex)
            {
                return Response.AsJson(new BasicResult(false, ex.Message), HttpStatusCode.InternalServerError);
            }
        }
    }
}
