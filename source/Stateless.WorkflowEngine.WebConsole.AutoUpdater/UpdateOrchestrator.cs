using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    public class UpdateOrchestrator
    {

        private IVersionComparisonService _versionComparisonService;

        public UpdateOrchestrator(IVersionComparisonService versionComparisonService)
        {
            _versionComparisonService = versionComparisonService;
        }


        public async Task<bool> Run()
        {
            Console.WriteLine("Checking for new version....");
            VersionComparisonResult versionComparisonResult = await  _versionComparisonService.CheckIfNewVersionAvailable();
            if (versionComparisonResult.IsNewVersionAvailable)
            {
                Console.WriteLine("New version available: {0}", versionComparisonResult.LatestReleaseVersionInfo.VersionNumber);
                // TODO: Download latest release with progress
                // TODO: Unzip latest release into temp folder
                // TODO: Stop current service
                // TODO: Uninstall current service
                // TODO: Backup current version
                // TODO: Delete all files other than data files
                // TODO: Copy new version files into the folder
                // TODO: Install new service
                // TODO: Start new service
                throw new NotImplementedException();
            }
            else
            {
                Console.WriteLine("Latest version already installed");
                return false;
            }



        }
    }
}
