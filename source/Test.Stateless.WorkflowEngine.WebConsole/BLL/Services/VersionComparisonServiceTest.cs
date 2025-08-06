using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using System;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class VersionComparisonServiceTest
    {
        private IWebConsoleVersionService _webConsoleVersionService;
        private IGitHubVersionService _gitHubVersionService;

        [SetUp]
        public void SetUp_VersionComparisonServiceTest()
        {
            _webConsoleVersionService = Substitute.For<IWebConsoleVersionService>();
            _gitHubVersionService = Substitute.For<IGitHubVersionService>();
    }

    [Test]
        public void CheckIfNewVersionAvailable_VersionsMatch_SetsValuesCorrectly()
        {
            string latestVersionUrl = Guid.NewGuid().ToString();
            const string version = "2.1.2";

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = version;

            _webConsoleVersionService.GetWebConsoleVersion().Returns(version);
            _gitHubVersionService.GetVersionInfo(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(latestVersionUrl, _webConsoleVersionService, _gitHubVersionService);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _gitHubVersionService.Received(1).GetVersionInfo(latestVersionUrl);
            Assert.That(result.IsNewVersionAvailable, Is.False);
            Assert.That(result.LatestReleaseVersionInfo.VersionNumber, Is.EqualTo(version));
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
            _gitHubVersionService.GetVersionInfo(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(latestVersionUrl, _webConsoleVersionService, _gitHubVersionService);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _gitHubVersionService.Received(1).GetVersionInfo(latestVersionUrl);
            Assert.That(result.IsNewVersionAvailable, Is.True);
            Assert.That(result.LatestReleaseVersionInfo.VersionNumber, Is.EqualTo(versionLatest));
        }

    }
}
