using Microsoft.Extensions.Caching.Memory;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Test.Stateless.WorkflowEngine.WebConsole.Services
{
    [TestFixture]
    public class VersionCheckServiceTests
    {
        private IAppSettings _appSettings = null!;
        private MemoryCache _memoryCache = null!;
        private IWebConsoleVersionService _versionService = null!;
        private VersionCheckService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _appSettings = Substitute.For<IAppSettings>();
            _appSettings.LatestVersionUrl.Returns("https://example.com/latest");
            _appSettings.UpdateCheckIntervalInMinutes.Returns(60);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _versionService = Substitute.For<IWebConsoleVersionService>();
            _service = new VersionCheckService(_appSettings, _memoryCache, _versionService);
        }

        [TearDown]
        public void TearDown()
        {
            _memoryCache.Dispose();
        }

        [TestCase("5.1.0", "5.1.0", false)]
        [TestCase("5.1.0", "5.0.9", false)]
        [TestCase("5.1.0", "5.1.1", true)]
        [TestCase("5.1.0", "6.0.0", true)]
        [TestCase("5.1.0", "v6.0.0", true)]
        [TestCase("5.1.0", "v5.1.0", false)]
        public async Task CheckIfNewVersionAvailable_ComparesVersions(string installed, string latest, bool expected)
        {
            SetVersions(installed, latest);

            var result = await _service.CheckIfNewVersionAvailable();

            Assert.That(result.IsNewVersionAvailable, Is.EqualTo(expected));
            Assert.That(result.InstalledVersionNumber, Is.EqualTo(installed));
            Assert.That(result.LatestReleaseVersionNumber, Is.EqualTo(latest.TrimStart('v')));
        }

        [Test]
        public async Task CheckIfNewVersionAvailable_ReturnsFalse_WhenTagIsNotAVersion()
        {
            SetVersions("5.1.0", "latest");

            var result = await _service.CheckIfNewVersionAvailable();

            Assert.That(result.IsNewVersionAvailable, Is.False);
        }

        [Test]
        public async Task CheckIfNewVersionAvailable_UsesCache_OnSecondCall()
        {
            SetVersions("5.1.0", "6.0.0");

            await _service.CheckIfNewVersionAvailable();
            var result = await _service.CheckIfNewVersionAvailable();

            Assert.That(result.IsNewVersionAvailable, Is.True);
            await _versionService.Received(1).GetLatestVersion("https://example.com/latest");
        }

        [Test]
        public async Task CheckIfNewVersionAvailable_Throws_WhenGitHubFails()
        {
            _versionService.GetWebConsoleVersion().Returns("5.1.0");
            _versionService.GetLatestVersion(Arg.Any<string>()).Returns(_ => Task.FromException<WebConsoleVersionInfo>(new HttpRequestException("rate limited")));

            await Assert.ThrowsAsync<HttpRequestException>(() => _service.CheckIfNewVersionAvailable());
        }

        [Test]
        public async Task CheckIfNewVersionAvailable_DoesNotCallGitHubAgain_AfterFailure()
        {
            _versionService.GetWebConsoleVersion().Returns("5.1.0");
            _versionService.GetLatestVersion(Arg.Any<string>()).Returns(_ => Task.FromException<WebConsoleVersionInfo>(new HttpRequestException("rate limited")));

            await Assert.ThrowsAsync<HttpRequestException>(() => _service.CheckIfNewVersionAvailable());
            var result = await _service.CheckIfNewVersionAvailable();

            Assert.That(result.InstalledVersionNumber, Is.EqualTo("5.1.0"));
            Assert.That(result.IsNewVersionAvailable, Is.False);
            await _versionService.Received(1).GetLatestVersion("https://example.com/latest");
        }

        [TestCase("6.0.0", "6.0.0")]
        [TestCase("v6.0.0", "6.0.0")]
        [TestCase(" V6.0.1 ", "6.0.1")]
        [TestCase("6.0.0-beta1", "6.0.0")]
        [TestCase(null, null)]
        public void NormaliseVersion_StripsPrefixAndSuffix(string? tag, string? expected)
        {
            Assert.That(VersionCheckService.NormaliseVersion(tag), Is.EqualTo(expected));
        }

        private void SetVersions(string installed, string latest)
        {
            _versionService.GetWebConsoleVersion().Returns(installed);
            _versionService.GetLatestVersion(Arg.Any<string>()).Returns(new WebConsoleVersionInfo { VersionNumber = latest });
        }
    }
}
