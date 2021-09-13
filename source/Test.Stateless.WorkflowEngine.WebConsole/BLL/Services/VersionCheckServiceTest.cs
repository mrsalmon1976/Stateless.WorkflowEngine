using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public void CheckIfNewVersionAvailable_OnExecute_ReturnsResult()
        {
            // setup
            VersionComparisonResult comparisonResult = new VersionComparisonResult();
            comparisonResult.LatestReleaseVersionInfo = new WebConsoleVersionInfo() { VersionNumber = "1.2.3" };
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(Task.FromResult<VersionComparisonResult>(comparisonResult));

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert

            Assert.AreEqual(comparisonResult.IsNewVersionAvailable, result.IsNewVersionAvailable);
            Assert.AreEqual(comparisonResult.LatestReleaseVersionInfo.VersionNumber, result.LatestReleaseVersionNumber);
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
            _memoryCache.TryGetValue<VersionCheckResult>(VersionCheckService.KeyCheckIfNewVersionAvailable, out cachedResult).Returns(false);
            int cacheMinutes = new Random().Next(1, 100);
            _appSettings.UpdateCheckIntervalInMinutes.Returns(cacheMinutes);

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert
            _memoryCache.Received(1).Set<VersionCheckResult>(VersionCheckService.KeyCheckIfNewVersionAvailable, Arg.Any<VersionCheckResult>(), TimeSpan.FromMinutes(cacheMinutes));
        }


        [Test]
        public void CheckIfNewVersionAvailable_ResultIsCached_DoesNotDoANewCheck()
        {
            // setup
            VersionCheckResult cachedResult = new VersionCheckResult();
            VersionComparisonResult comparisonResult = new VersionComparisonResult();
            comparisonResult.LatestReleaseVersionInfo = new WebConsoleVersionInfo() { VersionNumber = "1.2.3" };
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(Task.FromResult<VersionComparisonResult>(comparisonResult));
            _memoryCache.TryGetValue<VersionCheckResult>(VersionCheckService.KeyCheckIfNewVersionAvailable, out cachedResult).Returns(true);

            // execute
            VersionCheckResult result = _versionCheckService.CheckIfNewVersionAvailable();

            // assert
            _memoryCache.Received(0).Set<VersionCheckResult>(Arg.Any<string>(), Arg.Any<VersionCheckResult>(), Arg.Any<TimeSpan>());
        }

        #endregion
    }
}
