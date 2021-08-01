using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    public interface IVersionComparisonService
    {
        Task<VersionComparisonResult> CheckIfNewVersionAvailable();
    }

    public class VersionComparisonService : IVersionComparisonService
    {
        private readonly IAssemblyVersionChecker _assemblyVersionChecker;
        private readonly IWebVersionChecker _webVersionChecker;

        public VersionComparisonService(IAssemblyVersionChecker assemblyVersionChecker, IWebVersionChecker webVersionChecker)
        {
            this._assemblyVersionChecker = assemblyVersionChecker;
            this._webVersionChecker = webVersionChecker;
        }

        public async Task<VersionComparisonResult> CheckIfNewVersionAvailable()
        {
            string installedVersion = _assemblyVersionChecker.GetWebConsoleVersion();
            WebConsoleVersionInfo versionInfo = await _webVersionChecker.GetVersionInfo();
            string latestReleaseVersion = versionInfo.VersionNumber;

            VersionComparisonResult result = new VersionComparisonResult();
            result.IsNewVersionAvailable = (installedVersion != latestReleaseVersion);
            result.LatestReleaseVersionInfo = versionInfo;
            return result;
        }


    }
}
