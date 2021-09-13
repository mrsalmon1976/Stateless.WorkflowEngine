using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Hosting;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services
{
    public interface IInstallationService
    {
        void InstallService();

        void StartService();

        void StopService();

        void UninstallService();
    }

    public class InstallationService : IInstallationService
    {
        private readonly IUpdateLocationService _updateLocationService;
        private readonly IProcessWrapperFactory _processWrapperFactory;

        public InstallationService(IUpdateLocationService updateLocationService, IProcessWrapperFactory processWrapperFactory)
        {
            _updateLocationService = updateLocationService;
            _processWrapperFactory = processWrapperFactory;
        }

        public void InstallService()
        {
            this.RunProcess("install");
        }

        public void StartService()
        {
            this.RunProcess("start");
        }

        public void StopService()
        {
            this.RunProcess("stop");
        }

        public void UninstallService()
        {
            this.RunProcess("uninstall");
        }

        private void RunProcess(string commandArgument)
        {
            using (IProcessWrapper cmd = _processWrapperFactory.GetProcess())
            {
                cmd.StartInfo.WorkingDirectory = _updateLocationService.ApplicationFolder;
                cmd.StartInfo.FileName = UpdateConstants.WebConsoleExeFileName;
                cmd.StartInfo.Arguments = commandArgument;
                cmd.StartInfo.Verb = "runas";
                cmd.Start();
                cmd.WaitForExit();
                if (cmd.ExitCode != 0) 
                {
                    throw new ApplicationException(String.Format("Process exited with error code {0}", cmd.ExitCode));
                }
            }

        }
    }
}
