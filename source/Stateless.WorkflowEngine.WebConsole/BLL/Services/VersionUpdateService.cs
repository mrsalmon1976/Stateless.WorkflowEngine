using NLog;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Diagnostics;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Services
{
    public interface IVersionUpdateService
    {
        string ApplicationRootDirectory { get; set; }

        void DeleteInstallationTempFolders();

        void InstallUpdate();
    }
    
    public class VersionUpdateService : IVersionUpdateService
    {
        private readonly IProcessWrapperFactory _processWrapperFactory;
        private readonly IFileUtility _fileUtility;
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public VersionUpdateService(IProcessWrapperFactory processWrapper, IFileUtility fileUtility)
        {
            _processWrapperFactory = processWrapper;
            _fileUtility = fileUtility;
            this.ApplicationRootDirectory = AppContext.BaseDirectory;
        }

        public string ApplicationRootDirectory { get; set; }

        public void DeleteInstallationTempFolders()
        {
            string autoUpdaterShadowCopyFolder = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.AutoUpdaterShadowCopyFolderName);
            try
            {
                _fileUtility.DeleteDirectoryRecursive(autoUpdaterShadowCopyFolder);
            }
            catch (Exception exc)
            {
                // this exception can be ignored - it's not really an issue if the folder remains, and it 
                // will always fail on startup when the update process is running - this will only be successful
                // on the second startup of the service after the installation process
                _logger.Error(exc, exc.Message);
            }
        }

        public void InstallUpdate()
        {
            string autoUpdaterFolder = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.AutoUpdaterFolderName);
            string autoUpdaterShadowCopyFolder = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.AutoUpdaterShadowCopyFolderName);
            _logger.Info($"Autoupdater folder: {autoUpdaterFolder}, shadow copy folder {autoUpdaterShadowCopyFolder}");

            // copy the AutoUpdate folder to a ShadowCopy (allows it to also update!)
            _fileUtility.CopyRecursive(autoUpdaterFolder, autoUpdaterShadowCopyFolder);
            _logger.Info($"Autoupdater copied to shadow copy folder {autoUpdaterShadowCopyFolder}");

            using (IProcessWrapper process = _processWrapperFactory.GetProcess())
            {
                process.StartInfo.WorkingDirectory = autoUpdaterShadowCopyFolder;
                process.StartInfo.FileName = UpdateConstants.AutoUpdaterExeFileName;
                process.StartInfo.Verb = UpdateConstants.StartInfoVerb;
                bool isStarted = process.Start();
                _logger.Info($"Process start result {isStarted}");
            }
        }
    }
}
