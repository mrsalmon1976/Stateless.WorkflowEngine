using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    [TestFixture]
    public class VersionComparisonServiceTest
    {
        private IAppSettings _appSettings;
        private IWebConsoleVersionService _webConsoleVersionService;

        [SetUp]
        public void SetUp_VersionComparisonServiceTest()
        {
            _appSettings = Substitute.For<IAppSettings>();
            _webConsoleVersionService = Substitute.For<IWebConsoleVersionService>();
        }

        [Test]
        public void CheckIfNewVersionAvailable_VersionsMatch_SetsValuesCorrectly()
        {
            string latestVersionUrl = Guid.NewGuid().ToString();
            const string version = "2.1.2";

            _appSettings.LatestVersionUrl.Returns(latestVersionUrl);

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = version;

            _webConsoleVersionService.GetWebConsoleVersion().Returns(version);
            _webConsoleVersionService.GetLatestVersion(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(_appSettings, _webConsoleVersionService);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _webConsoleVersionService.Received(1).GetLatestVersion(latestVersionUrl);
            Assert.That(result.IsNewVersionAvailable, Is.False);
            Assert.That(result.LatestReleaseVersionInfo.VersionNumber, Is.EqualTo(version));
        }

        [Test]
        public void CheckIfNewVersionAvailable_VersionsDoNotMatch_SetsValuesCorrectly()
        {
            string latestVersionUrl = Guid.NewGuid().ToString();
            const string versionInstalled = "2.1.1";
            const string versionLatest = "2.1.3";

            _appSettings.LatestVersionUrl.Returns(latestVersionUrl);

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = versionLatest;

            _webConsoleVersionService.GetWebConsoleVersion().Returns(versionInstalled);
            _webConsoleVersionService.GetLatestVersion(latestVersionUrl).Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(_appSettings, _webConsoleVersionService);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            _webConsoleVersionService.Received(1).GetLatestVersion(latestVersionUrl);
            Assert.That(result.IsNewVersionAvailable, Is.True);
            Assert.That(result.LatestReleaseVersionInfo.VersionNumber, Is.EqualTo(versionLatest));
        }

    }
}
