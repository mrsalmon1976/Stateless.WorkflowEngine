using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    [TestFixture]
    public class UpdateOrchestratorTest
    {
        private IVersionComparisonService _versionComparisonService;
        private IUpdateLocationService _updateLocationService;
        private IUpdateDownloadService _updateDownloadService;
        private IUpdateFileService _updateFileService;
        private IInstallationService _installationService;
        private IUpdateEventLogger _updateEventLogger;

        private UpdateOrchestrator _updateOrchestrator;

        [SetUp]
        public void UpdateOrchestratorTest_SetUp()
        {
            _versionComparisonService = Substitute.For<IVersionComparisonService>();
            _updateLocationService = Substitute.For<IUpdateLocationService>();
            _updateDownloadService = Substitute.For<IUpdateDownloadService>();
            _updateFileService = Substitute.For<IUpdateFileService>();
            _installationService = Substitute.For<IInstallationService>();
            _updateEventLogger = Substitute.For<IUpdateEventLogger>();

            _updateOrchestrator = new UpdateOrchestrator(_versionComparisonService, _updateLocationService, _updateDownloadService, _updateFileService, _installationService, _updateEventLogger);
        }

        [Test]
        public void Run_NewVersionNotAvailable_ExitsWithoutDownload()
        {
            VersionComparisonResult versionComparisonResult = BuildVersionComparisonResult(false);
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(versionComparisonResult);

            bool result = _updateOrchestrator.Run().Result;

            // assert
            Assert.IsFalse(result);
            _versionComparisonService.Received(1).CheckIfNewVersionAvailable();
            _updateDownloadService.DidNotReceive().DownloadFile(Arg.Any<string>(), Arg.Any<string>());

        }

        [Test]
        public void Run_NewVersionIsAvailable_CheckExecutionSequence()
        {
            VersionComparisonResult versionComparisonResult = BuildVersionComparisonResult(true);
            WebConsoleVersionInfo latestVersionInfo = versionComparisonResult.LatestReleaseVersionInfo;
            _versionComparisonService.CheckIfNewVersionAvailable().Returns(versionComparisonResult);

            string updateTempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "__UpdateTemp");
            _updateLocationService.UpdateTempFolder.Returns(updateTempFolder);

            string downloadPath = Path.Combine(updateTempFolder, latestVersionInfo.FileName);

            bool result = _updateOrchestrator.Run().Result;

            // assert
            Assert.IsTrue(result);
            Received.InOrder(async () =>
            {
                await _versionComparisonService.Received(1).CheckIfNewVersionAvailable();
                _updateLocationService.EnsureEmptyUpdateTempFolderExists();
                await _updateDownloadService.DownloadFile(latestVersionInfo.DownloadUrl, downloadPath);
                await _updateFileService.ExtractReleasePackage(downloadPath, updateTempFolder);
                _installationService.StopService();
                _installationService.UninstallService();
                await _updateFileService.Backup();
                await _updateFileService.DeleteCurrentVersionFiles();
                await _updateFileService.CopyNewVersionFiles(latestVersionInfo.FileName);
                _installationService.InstallService();
                _installationService.StartService();
                _updateLocationService.DeleteUpdateTempFolder();
            });
        }

        private VersionComparisonResult BuildVersionComparisonResult(bool isNewVersionAvailable)
        {
            WebConsoleVersionInfo versionInfo = new WebConsoleVersionInfo();

            if (isNewVersionAvailable) 
            {
                versionInfo.FileName = Path.GetRandomFileName();
                versionInfo.DownloadUrl = "https://software.safish.com/stateless/latest";
            }

            VersionComparisonResult versionComparisonResult = new VersionComparisonResult();
            versionComparisonResult.IsNewVersionAvailable = isNewVersionAvailable;
            versionComparisonResult.LatestReleaseVersionInfo = versionInfo;
            return versionComparisonResult;
        }
    }
}
