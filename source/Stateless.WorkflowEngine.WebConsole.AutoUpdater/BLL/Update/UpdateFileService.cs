using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update
{
    public interface IUpdateFileService
    {
        Task Backup(string applicationPath, string backupfolder, string[] exclusions);

        Task ExtractReleasePackage(string filePath, string extractFolder);
    }

    public class UpdateFileService : IUpdateFileService
    {
        private readonly IFileUtility _fileUtility;

        public UpdateFileService(IFileUtility fileUtility)
        {
            this._fileUtility = fileUtility;
        }

        public async Task Backup(string applicationPath, string backupFolder, string[] exclusions)
        {
            await Task.Run(() => {
                if (Directory.Exists(backupFolder))
                {
                    Directory.Delete(backupFolder, true);
                }
                _fileUtility.CopyRecursive(applicationPath, backupFolder, exclusions);
            });
        }

        public async Task ExtractReleasePackage(string filePath, string extractFolder)
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(filePath, extractFolder));
        }
    }
}
