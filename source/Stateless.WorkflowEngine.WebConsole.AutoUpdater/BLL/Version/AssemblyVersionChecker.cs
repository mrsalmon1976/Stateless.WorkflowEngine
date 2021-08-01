using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version
{
    public interface IAssemblyVersionChecker
    {
        string WebConsoleExeFileName { get; set; }
        string GetWebConsoleVersion();
    }

    public class AssemblyVersionChecker : IAssemblyVersionChecker
    {

        public AssemblyVersionChecker()
        {
            this.WebConsoleExeFileName = "Stateless.WorkflowEngine.WebConsole.exe";
        }

        public string WebConsoleExeFileName { get; set; }

        public string GetWebConsoleVersion()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string pathToExe = Path.Combine(folder, this.WebConsoleExeFileName);
            if (!File.Exists(pathToExe))
            {
                throw new FileNotFoundException(String.Format("Unable to find web console executable {0}", pathToExe));
            }
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            return System.Version.Parse(versionInfo.FileVersion).ToString(3);
        }
    }
}
