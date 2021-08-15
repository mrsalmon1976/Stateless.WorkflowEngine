using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
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
    public interface IAssemblyVersionService
    {
        string WebConsoleExeFileName { get; set; }

        string GetWebConsoleVersion();
    }

    public class AssemblyVersionService : IAssemblyVersionService
    {
        private readonly IUpdateLocationService _updateFileService;

        public AssemblyVersionService(IUpdateLocationService updateFileService)
        {
            this.WebConsoleExeFileName = AutoUpdaterConstants.WebConsoleExeFileName;
            this._updateFileService = updateFileService;
        }

        public string WebConsoleExeFileName { get; set; }

        public string GetWebConsoleVersion()
        {
            string pathToExe = Path.Combine(_updateFileService.ApplicationFolder, this.WebConsoleExeFileName);
            if (!File.Exists(pathToExe))
            {
                throw new FileNotFoundException(String.Format("Unable to find web console executable {0}", pathToExe));
            }
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            return System.Version.Parse(versionInfo.FileVersion).ToString(3);
        }
    }
}
