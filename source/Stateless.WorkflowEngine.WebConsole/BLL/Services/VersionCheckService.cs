using Microsoft.Extensions.Caching.Memory;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.Caching;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IVersionCheckService
    {
        VersionCheckResult CheckIfNewVersionAvailable();
    }

    public class VersionCheckService : IVersionCheckService
    {
        private readonly IAppSettings _appSettings;
        private readonly IMemoryCache _memoryCache;
        private readonly IVersionComparisonService _versionComparisonService;

        public VersionCheckService(IAppSettings appSettings, IMemoryCache memoryCache, IVersionComparisonService versionComparisonService)
        {
            _appSettings = appSettings;
            _memoryCache = memoryCache;
            _versionComparisonService = versionComparisonService;
        }

        public VersionCheckResult CheckIfNewVersionAvailable()
        {
            VersionCheckResult result;
            if (_memoryCache.TryGetValue<VersionCheckResult>(CacheKeys.CheckIfNewVersionAvailable, out result))
            {
                return result;
            }

            VersionComparisonResult comparisonResult = _versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();
            result = new VersionCheckResult();
            result.IsNewVersionAvailable = comparisonResult.IsNewVersionAvailable;
            result.LatestReleaseVersionNumber = comparisonResult.LatestReleaseVersionInfo.VersionNumber;
            _memoryCache.Set<VersionCheckResult>(CacheKeys.CheckIfNewVersionAvailable, result, TimeSpan.FromMinutes(_appSettings.UpdateCheckIntervalInMinutes));
            return result;
        }
    }
}
