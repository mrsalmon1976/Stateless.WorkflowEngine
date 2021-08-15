using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public interface IVersionComparisonService
    {
        Task<VersionComparisonResult> CheckIfNewVersionAvailable();
    }

    public class VersionComparisonService : IVersionComparisonService
    {
        private readonly IAssemblyVersionService _assemblyVersionService;
        private readonly IWebVersionService _webVersionService;

        public VersionComparisonService(IAssemblyVersionService assemblyVersionService, IWebVersionService webVersionService)
        {
            this._assemblyVersionService = assemblyVersionService;
            this._webVersionService = webVersionService;
        }

        public async Task<VersionComparisonResult> CheckIfNewVersionAvailable()
        {
            string installedVersion = _assemblyVersionService.GetWebConsoleVersion();
            WebConsoleVersionInfo versionInfo = await _webVersionService.GetVersionInfo();
            string latestReleaseVersion = versionInfo.VersionNumber;

            VersionComparisonResult result = new VersionComparisonResult();
            result.IsNewVersionAvailable = (installedVersion != latestReleaseVersion);
            result.LatestReleaseVersionInfo = versionInfo;
            return result;
        }


    }
}
