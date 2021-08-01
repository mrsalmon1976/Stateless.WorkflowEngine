using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    [TestFixture]
    public class VersionComparisonServiceTest
    {
        [Test]
        public void CheckIfNewVersionAvailable_VersionsMatch_SetsValuesCorrectly()
        {
            const string version = "2.1.2";
            IAssemblyVersionChecker assemblyVersionChecker = Substitute.For<IAssemblyVersionChecker>();
            IWebVersionChecker webVersionChecker = Substitute.For<IWebVersionChecker>();

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = version;
            
            assemblyVersionChecker.GetWebConsoleVersion().Returns(version);
            webVersionChecker.GetVersionInfo().Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(assemblyVersionChecker, webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            Assert.IsFalse(result.IsNewVersionAvailable);
            Assert.AreEqual(version, result.LatestReleaseVersionInfo.VersionNumber);
        }

        [Test]
        public void CheckIfNewVersionAvailable_VersionsDoNotMatch_SetsValuesCorrectly()
        {
            const string versionInstalled = "2.1.1";
            const string versionLatest = "2.1.3";
            IAssemblyVersionChecker assemblyVersionChecker = Substitute.For<IAssemblyVersionChecker>();
            IWebVersionChecker webVersionChecker = Substitute.For<IWebVersionChecker>();

            WebConsoleVersionInfo webConsoleVersionInfo = new WebConsoleVersionInfo();
            webConsoleVersionInfo.VersionNumber = versionLatest;

            assemblyVersionChecker.GetWebConsoleVersion().Returns(versionInstalled);
            webVersionChecker.GetVersionInfo().Returns(Task.FromResult(webConsoleVersionInfo));

            VersionComparisonService versionComparisonService = new VersionComparisonService(assemblyVersionChecker, webVersionChecker);
            VersionComparisonResult result = versionComparisonService.CheckIfNewVersionAvailable().GetAwaiter().GetResult();

            Assert.IsTrue(result.IsNewVersionAvailable);
            Assert.AreEqual(versionLatest, result.LatestReleaseVersionInfo.VersionNumber);
        }

    }
}
