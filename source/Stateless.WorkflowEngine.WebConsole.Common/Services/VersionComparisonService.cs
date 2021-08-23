using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Services
{
    public interface IVersionComparisonService
    {
        Task<VersionComparisonResult> CheckIfNewVersionAvailable();
    }

    public class VersionComparisonService : IVersionComparisonService
    {
        private readonly string _latestVersionUrl;
        private readonly IWebConsoleVersionService _webConsoleVersionService;
        private readonly IGitHubVersionService _gitHubVersionService;

        public VersionComparisonService(string latestVersionUrl, IWebConsoleVersionService webConsoleVersionService, IGitHubVersionService webVersionService)
        {
            _latestVersionUrl = latestVersionUrl;
            this._webConsoleVersionService = webConsoleVersionService;
            this._gitHubVersionService = webVersionService;
        }

        public async Task<VersionComparisonResult> CheckIfNewVersionAvailable()
        {
            string installedVersion = _webConsoleVersionService.GetWebConsoleVersion();
            WebConsoleVersionInfo versionInfo = await _gitHubVersionService.GetVersionInfo(_latestVersionUrl);
            string latestReleaseVersion = versionInfo.VersionNumber;

            VersionComparisonResult result = new VersionComparisonResult();
            result.IsNewVersionAvailable = (installedVersion != latestReleaseVersion);
            result.LatestReleaseVersionInfo = versionInfo;
            return result;
        }


    }
}
