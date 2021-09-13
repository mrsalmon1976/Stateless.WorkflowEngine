using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public interface IUpdateFileService
    {
        Task Backup();

        Task CopyNewVersionFiles();

        Task DeleteCurrentVersionFiles();

        Task ExtractReleasePackage(string filePath, string extractFolder);
    }

    public class UpdateFileService : IUpdateFileService
    {
        private readonly IUpdateLocationService _updateLocationService;
        private readonly IFileUtility _fileUtility;

        public UpdateFileService(IUpdateLocationService updateLocationService, IFileUtility fileUtility)
        {
            _updateLocationService = updateLocationService;
            _fileUtility = fileUtility;
        }

        public async Task Backup()
        {
            await Task.Run(() => {
                _fileUtility.DeleteDirectoryRecursive(_updateLocationService.BackupFolder);
                string[] exclusions = { _updateLocationService.BackupFolder, _updateLocationService.UpdateTempFolder };
                _fileUtility.CopyRecursive(_updateLocationService.ApplicationFolder, _updateLocationService.BackupFolder, exclusions);
            });
        }

        public async Task CopyNewVersionFiles()
        {
            await Task.Run(() => {
                _fileUtility.CopyRecursive(_updateLocationService.UpdateTempFolder, _updateLocationService.ApplicationFolder, new string[] { });
            });
        }

        public async Task DeleteCurrentVersionFiles()
        {
            await Task.Run(() => {

                string[] exclusions = { 
                    _updateLocationService.BackupFolder
                    , _updateLocationService.UpdateTempFolder
                    , _updateLocationService.DataFolder 
                    , _updateLocationService.UpdateEventLogFilePath
                    , _updateLocationService.AutoUpdaterFolder
                };
                _fileUtility.DeleteContents(_updateLocationService.ApplicationFolder, exclusions);
            });
        }

        public async Task ExtractReleasePackage(string filePath, string extractFolder)
        {
            await Task.Run(() => _fileUtility.ExtractZipFile(filePath, extractFolder));
        }
    }
}
