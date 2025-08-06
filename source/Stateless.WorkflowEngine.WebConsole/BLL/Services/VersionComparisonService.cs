using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IVersionComparisonService
    {
        Task<VersionComparisonResult> CheckIfNewVersionAvailable();
    }

    public class VersionComparisonService : IVersionComparisonService
    {
        private readonly IAppSettings _appSettings;
        private readonly IWebConsoleVersionService _webConsoleVersionService;

        public VersionComparisonService(IAppSettings appSettings, IWebConsoleVersionService webConsoleVersionService)
        {
            this._appSettings = appSettings;
            this._webConsoleVersionService = webConsoleVersionService;
        }

        public async Task<VersionComparisonResult> CheckIfNewVersionAvailable()
        {
            string installedVersion = _webConsoleVersionService.GetWebConsoleVersion();
            WebConsoleVersionInfo versionInfo = await _webConsoleVersionService.GetLatestVersion(_appSettings.LatestVersionUrl);
            string latestReleaseVersion = versionInfo.VersionNumber;

            var vInstalled = Version.Parse(installedVersion);
            var vLatest = Version.Parse(latestReleaseVersion);

            VersionComparisonResult result = new VersionComparisonResult();
            result.IsNewVersionAvailable = (vInstalled < vLatest);
            result.LatestReleaseVersionInfo = versionInfo;
            return result;
        }


    }
}
