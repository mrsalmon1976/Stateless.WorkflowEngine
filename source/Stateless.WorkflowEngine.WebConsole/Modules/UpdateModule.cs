using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using Nancy.ModelBinding;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Encryption;
using System.Diagnostics;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using NLog;

namespace Stateless.WorkflowEngine.WebConsole.Modules
{
    public class UpdateModule : WebConsoleSecureModule
    {
        private readonly IVersionUpdateService _versionUpdateService;
        private readonly IVersionCheckService _versionCheckService;
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public UpdateModule(IVersionUpdateService updateService, IVersionCheckService versionCheckService)
            : base()
        {
            _versionUpdateService = updateService;
            _versionCheckService = versionCheckService;

            Get[Actions.Update.CheckForUpdate] = (x) =>
            {
                return this.CheckForUpdate();
            };
            Get[Actions.Update.Index] = (x) =>
            {
                AddScript(Scripts.UpdateView);
                return this.View[Views.Update.Index];
            };
            Get[Actions.Update.Install] = (x) =>
            {
                return Install();
            };
        }


        public dynamic CheckForUpdate()
        {
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();
            return this.Response.AsJson<VersionCheckResult>(result);
        }

        public dynamic Install()
        {
            _logger.Info("UpdateModule preparing to install");
            _versionUpdateService.InstallUpdate();
            return this.Response.AsRedirect(Actions.Update.Index, Nancy.Responses.RedirectResponse.RedirectType.SeeOther);
        }

    }
}
