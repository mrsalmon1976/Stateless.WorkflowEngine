using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public class AssemblyVersionService : IWebConsoleVersionService
    {
        private readonly string _webConsoleExeFileName;
        private readonly IUpdateLocationService _updateFileService;

        public AssemblyVersionService(string webConsoleExeFileName, IUpdateLocationService updateFileService)
        {
            _webConsoleExeFileName = AutoUpdaterConstants.WebConsoleExeFileName;
            this._updateFileService = updateFileService;
        }

        public string GetWebConsoleVersion()
        {
            string pathToExe = Path.Combine(_updateFileService.ApplicationFolder, this._webConsoleExeFileName);
            if (!File.Exists(pathToExe))
            {
                throw new FileNotFoundException(String.Format("Unable to find web console executable {0}", pathToExe));
            }
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            return System.Version.Parse(versionInfo.FileVersion).ToString(3);
        }
    }
}
