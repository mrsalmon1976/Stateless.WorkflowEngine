using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Caching;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class VersionCheckServiceTest
    {
        private IAppSettings _appSettings;
        private IMemoryCache _memoryCache;
        private IVersionComparisonService _versionComparisonService;

        private IVersionCheckService _versionCheckService;

        [SetUp]
        public void WorkflowStoreInfoServiceTest_SetUp()
        {
            _appSettings = Substitute.For<IAppSettings>();
            _memoryCache = Substitute.For<IMemoryCache>();
            _versionComparisonService = Substitute.For<IVersionComparisonService>();

            _versionCheckService = new VersionCheckService(_appSettings, _memoryCache, _versionComparisonService);
        }

        #region CheckIfNewVersionAvailable Tests

        [Test]
        public void CheckIfNewVersionAvailable_OnExecute_VerifyResult()
        {
            // setup
            VersionComparisonResult comparisonResult = new VersionComparisonResult();
            comparisonResult.LatestReleaseVersionInfo = new WebConsoleVersionInfo() { VersionNumber = "1.2.3" };
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(Task.FromResult<VersionComparisonResult>(comparisonResult));

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert

            Assert.That(result.IsNewVersionAvailable, Is.EqualTo(comparisonResult.IsNewVersionAvailable));
            Assert.That(result.LatestReleaseVersionNumber, Is.EqualTo(comparisonResult.LatestReleaseVersionInfo.VersionNumber));
            _versionComparisonService.Received(1).CheckIfNewVersionAvailable();

        }

        [Test]
        public void CheckIfNewVersionAvailable_ResultNotCached_AddsToCache()
        {
            // setup
            VersionCheckResult cachedResult;// = Arg.Any<VersionCheckResult>();
            VersionComparisonResult comparisonResult = new VersionComparisonResult();
            comparisonResult.LatestReleaseVersionInfo = new WebConsoleVersionInfo() { VersionNumber = "1.2.3" };
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(Task.FromResult<VersionComparisonResult>(comparisonResult));
            _memoryCache.TryGetValue<VersionCheckResult>(CacheKeys.CheckIfNewVersionAvailable, out cachedResult).Returns(false);
            int cacheMinutes = new Random().Next(1, 100);
            _appSettings.UpdateCheckIntervalInMinutes.Returns(cacheMinutes);

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert
            _memoryCache.Received(1).Set<VersionCheckResult>(CacheKeys.CheckIfNewVersionAvailable, result, TimeSpan.FromMinutes(cacheMinutes));
            _versionComparisonService.Received(1).CheckIfNewVersionAvailable();
        }


        [Test]
        public void CheckIfNewVersionAvailable_ResultIsCached_DoesNotDoANewCheck()
        {
            // setup
            VersionCheckResult cachedResult = new VersionCheckResult();
            VersionComparisonResult comparisonResult = new VersionComparisonResult();
            comparisonResult.LatestReleaseVersionInfo = new WebConsoleVersionInfo() { VersionNumber = "1.2.3" };
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(Task.FromResult<VersionComparisonResult>(comparisonResult));
            _memoryCache.TryGetValue<VersionCheckResult>(CacheKeys.CheckIfNewVersionAvailable, out cachedResult).Returns(true);

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert
            _versionComparisonService.DidNotReceive().CheckIfNewVersionAvailable();
        }

        #endregion
    }
}
