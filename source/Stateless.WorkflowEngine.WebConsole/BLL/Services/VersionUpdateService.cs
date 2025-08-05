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

        public void InstallUpdate()
        {
            string scriptPath = Path.Combine(this.ApplicationRootDirectory, UpdateConstants.UpdaterFileName);
            _logger.Info($"Update location: '{scriptPath}'");

            using (IProcessWrapper process = _processWrapperFactory.GetProcess())
            {
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WorkingDirectory = this.ApplicationRootDirectory;
                process.StartInfo.Verb = UpdateConstants.StartInfoVerb;
                bool isStarted = process.Start();
                _logger.Info($"Process start result: isStarted");
            }
        }
    }
}
