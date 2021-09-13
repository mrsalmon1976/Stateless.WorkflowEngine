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

        void InstallUpdate();
    }
    
    public class VersionUpdateService : IVersionUpdateService
    {
        private readonly IProcessWrapperFactory _processWrapperFactory;
        private readonly IFileUtility _fileUtility;

        public VersionUpdateService(IProcessWrapperFactory processWrapper, IFileUtility fileUtility)
        {
            _processWrapperFactory = processWrapper;
            _fileUtility = fileUtility;
            this.ApplicationRootDirectory = AppContext.BaseDirectory;
        }

        public string ApplicationRootDirectory { get; set; }

        public void InstallUpdate()
        {
            string autoUpdaterFolder = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.AutoUpdaterFolderName);
            string autoUpdaterShadowCopyFolder = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.AutoUpdaterShadowCopyFolderName);

            // copy the AutoUpdate folder to a ShadowCopy (allows it to also update!)
            _fileUtility.CopyRecursive(autoUpdaterFolder, autoUpdaterShadowCopyFolder);

            using (IProcessWrapper process = _processWrapperFactory.GetProcess())
            {
                process.StartInfo.WorkingDirectory = autoUpdaterShadowCopyFolder;
                process.StartInfo.FileName = UpdateConstants.AutoUpdaterExeFileName;
                process.StartInfo.Verb = UpdateConstants.StartInfoVerb;
                process.Start();
            }
        }
    }
}
