using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    [TestFixture]
    public class VersionComparisonServiceTest
    {
        [Test]
        public void CheckIfNewVersionAvailable_VersionsMatch_SetsValuesCorrectly()
        {
            const string version = "2.1.2";
            IAssemblyVersionService assemblyVersionService = Substitute.For<IAssemblyVersionService>();
            IWebVersionService webVersionChecker = Substitute.For<IWebVersionService>();

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = version;
            
            assemblyVersionService.GetWebConsoleVersion().Returns(version);
            webVersionChecker.GetVersionInfo().Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(assemblyVersionService, webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            Assert.IsFalse(result.IsNewVersionAvailable);
            Assert.AreEqual(version, result.LatestReleaseVersionInfo.VersionNumber);
        }

        [Test]
        public void CheckIfNewVersionAvailable_VersionsDoNotMatch_SetsValuesCorrectly()
        {
            const string versionInstalled = "2.1.1";
            const string versionLatest = "2.1.3";
            IAssemblyVersionService assemblyVersionService = Substitute.For<IAssemblyVersionService>();
            IWebVersionService webVersionChecker = Substitute.For<IWebVersionService>();

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = versionLatest;

            assemblyVersionService.GetWebConsoleVersion().Returns(versionInstalled);
            webVersionChecker.GetVersionInfo().Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(assemblyVersionService, webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            Assert.IsTrue(result.IsNewVersionAvailable);
            Assert.AreEqual(versionLatest, result.LatestReleaseVersionInfo.VersionNumber);
        }

    }
}
