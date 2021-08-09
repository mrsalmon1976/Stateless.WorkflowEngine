using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Hosting;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services.Windows
{
    public interface IInstallationService
    {
        void StopService();
        void UninstallService();
    }

    public class InstallationService : IInstallationService
    {
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
            using (Process cmd = new Process())
            {
                cmd.StartInfo.WorkingDirectory = "C:\\Temp\\Stateless";
                cmd.StartInfo.FileName = "Stateless.WorkflowEngine.WebConsole.exe";
                cmd.StartInfo.Arguments = commandArgument;
                //cmd.StartInfo.RedirectStandardOutput = true;
                //cmd.StartInfo.CreateNoWindow = true;
                
                //cmd.StartInfo.UseShellExecute = false;
                //cmd.StartInfo.RedirectStandardInput = true;
                //cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.Verb = "runas";

                //cmd.StartInfo.
                
                cmd.Start();
                //string outPut = cmd.StandardOutput.ReadToEnd();
                //string error = cmd.StandardError.ReadToEnd();

                //cmd.WaitForInputIdle(5000);
                //System.Threading.Thread.Sleep(20000);
                cmd.WaitForExit();
                Console.WriteLine("Process complete with exit code {0}", cmd.ExitCode);
            }

        }
    }
}
