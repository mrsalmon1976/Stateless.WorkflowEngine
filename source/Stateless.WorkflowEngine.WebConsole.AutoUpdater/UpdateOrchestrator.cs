using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    public class UpdateOrchestrator
    {

        private readonly IVersionComparisonService _versionComparisonService;
        private readonly IUpdateLocationService _updateLocationService;
        private readonly IUpdateDownloadService _updateDownloadService;
        private readonly IUpdateFileService _updateFileService;
        private readonly IInstallationService _installationService;
        private readonly IUpdateEventLogger _updateEventLogger;

        public UpdateOrchestrator(IVersionComparisonService versionComparisonService
            , IUpdateLocationService updateLocationService
            , IUpdateDownloadService updateDownloadService
            , IUpdateFileService updateFileService
            , IInstallationService installationService
            , IUpdateEventLogger updateEventLogger
            )
        {
            this._versionComparisonService = versionComparisonService;
            this._updateLocationService = updateLocationService;
            this._updateDownloadService = updateDownloadService;
            this._updateFileService = updateFileService;
            this._installationService = installationService;
            this._updateEventLogger = updateEventLogger;
        }


        public async Task<bool> Run()
        {
            _updateEventLogger.ClearLogFile();

            _updateEventLogger.LogLine($"Updater start time {DateTime.Now}.");
            _updateEventLogger.Log($"Checking for new version (location: {_updateLocationService.ApplicationFolder})...");
            VersionComparisonResult versionComparisonResult = await  _versionComparisonService.CheckIfNewVersionAvailable();
            _updateEventLogger.LogLine("done.");

            WebConsoleVersionInfo latestVersionInfo = versionComparisonResult.LatestReleaseVersionInfo;
            bool updated = false;

            if (versionComparisonResult.IsNewVersionAvailable)
            {
                _updateEventLogger.LogLine($"New version available: {latestVersionInfo.VersionNumber}");

                _updateEventLogger.Log($"Creating temporary update folder {_updateLocationService.UpdateTempFolder}...");
                _updateLocationService.EnsureEmptyUpdateTempFolderExists();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log($"Downloading file from {latestVersionInfo.DownloadUrl}...");
                string downloadPath = Path.Combine(_updateLocationService.UpdateTempFolder, latestVersionInfo.FileName);
                await _updateDownloadService.DownloadFile(latestVersionInfo.DownloadUrl, downloadPath);
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log($"Extracting release contents to {_updateLocationService.UpdateTempFolder}...");
                await _updateFileService.ExtractReleasePackage(downloadPath, _updateLocationService.UpdateTempFolder);
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Stopping installed service...");
                _installationService.StopService();
                _updateEventLogger.LogLine("done.");
                _updateEventLogger.Log("Uninstalling service...");
                _installationService.UninstallService();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Backing up current service files...");
                await _updateFileService.Backup();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Deleting current service files...");
                await _updateFileService.DeleteCurrentVersionFiles();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Copying new service files...");
                await _updateFileService.CopyNewVersionFiles();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Installing service...");
                _installationService.InstallService();
                _updateEventLogger.LogLine("done.");

                _updateEventLogger.Log("Starting service...");
                _installationService.StartService();
                _updateEventLogger.LogLine("done.");

                // cleanup!
                _updateEventLogger.Log($"Cleaning up temp update folder {_updateLocationService.UpdateTempFolder}...");
                _updateLocationService.DeleteUpdateTempFolder();
                _updateEventLogger.LogLine("done.");

                System.Threading.Thread.Sleep(5000);
                _updateEventLogger.LogLine("Installation complete.");
                updated = true;
            }
            else
            {
                _updateEventLogger.LogLine($"Latest version already installed ({latestVersionInfo.VersionNumber})");
                updated = false;
            }
            _updateEventLogger.LogLine($"Updater finish time {DateTime.Now}.");
            return updated;

        }
    }
}
