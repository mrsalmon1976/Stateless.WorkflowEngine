using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services.Windows;
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

        public UpdateOrchestrator(IVersionComparisonService versionComparisonService
            , IUpdateLocationService updateLocationService
            , IUpdateDownloadService updateDownloadService
            , IUpdateFileService updateFileService
            , IInstallationService installationService
            )
        {
            this._versionComparisonService = versionComparisonService;
            this._updateLocationService = updateLocationService;
            this._updateDownloadService = updateDownloadService;
            this._updateFileService = updateFileService;
            this._installationService = installationService;
        }


        public async Task<bool> Run()
        {
            Console.WriteLine("Checking for new version....");
            VersionComparisonResult versionComparisonResult = await  _versionComparisonService.CheckIfNewVersionAvailable();
            WebConsoleVersionInfo latestVersionInfo = versionComparisonResult.LatestReleaseVersionInfo;
            if (1 == 1 || versionComparisonResult.IsNewVersionAvailable)
            {
                /*
                Console.WriteLine("New version available: {0}", latestVersionInfo.VersionNumber);

                Console.WriteLine("Creating temporary update folder {0}", _updateLocationService.UpdateTempFolder);
                await _updateLocationService.EnsureEmptyUpdateTempFolderExists();

                Console.WriteLine("Downloading file from {0}", latestVersionInfo.DownloadUrl);
                string downloadPath = Path.Combine(_updateLocationService.UpdateTempFolder, latestVersionInfo.FileName);
                await _updateDownloadService.DownloadFile(latestVersionInfo.DownloadUrl, downloadPath);

                Console.WriteLine("Extracting release contents to {0}", _updateLocationService.UpdateTempFolder);
                await _updateFileService.ExtractReleasePackage(downloadPath, _updateLocationService.UpdateTempFolder);
                */

                _installationService.StopService();
                _installationService.UninstallService();

                await _updateFileService.Backup(_updateLocationService.BaseFolder, _updateLocationService.BackupFolder, new string[] { _updateLocationService.BackupFolder, _updateLocationService.UpdateTempFolder });

                // TODO: Delete all files other than data files
                // TODO: Copy new version files into the folder
                // TODO: Install new service
                // TODO: Start new service

                // cleanup!
                Console.WriteLine("Cleaning up temp update folder {0}", _updateLocationService.UpdateTempFolder);
                await _updateLocationService.DeleteUpdateTempFolder();

                Console.ReadLine();
                return true;
            }
            else
            {
                Console.WriteLine("Latest version already installed ({0})", latestVersionInfo.VersionNumber);
                return false;
            }



        }
    }
}
