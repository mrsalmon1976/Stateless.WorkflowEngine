using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Common.Services
{
    [TestFixture]
    public class VersionComparisonServiceTest
    {
        private IWebConsoleVersionService _webConsoleVersionService;
        private IWebVersionService _webVersionChecker;

        [SetUp]
        public void SetUp_VersionComparisonServiceTest()
        {
            _webConsoleVersionService = Substitute.For<IWebConsoleVersionService>();
            _webVersionChecker = Substitute.For<IWebVersionService>();
    }

    [Test]
        public void CheckIfNewVersionAvailable_VersionsMatch_SetsValuesCorrectly()
        {
            string latestVersionUrl = Guid.NewGuid().ToString();
            const string version = "2.1.2";

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = version;

            _webConsoleVersionService.GetWebConsoleVersion().Returns(version);
            _webVersionChecker.GetVersionInfo(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(latestVersionUrl, _webConsoleVersionService, _webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _webVersionChecker.Received(1).GetVersionInfo(latestVersionUrl);
            Assert.IsFalse(result.IsNewVersionAvailable);
            Assert.AreEqual(version, result.LatestReleaseVersionInfo.VersionNumber);
        }

        [Test]
        public void CheckIfNewVersionAvailable_VersionsDoNotMatch_SetsValuesCorrectly()
        {
            string latestVersionUrl = Guid.NewGuid().ToString();
            const string versionInstalled = "2.1.1";
            const string versionLatest = "2.1.3";

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = versionLatest;

            _webConsoleVersionService.GetWebConsoleVersion().Returns(versionInstalled);
            _webVersionChecker.GetVersionInfo(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(latestVersionUrl, _webConsoleVersionService, _webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _webVersionChecker.Received(1).GetVersionInfo(latestVersionUrl);
            Assert.IsTrue(result.IsNewVersionAvailable);
            Assert.AreEqual(versionLatest, result.LatestReleaseVersionInfo.VersionNumber);
        }

    }
}
