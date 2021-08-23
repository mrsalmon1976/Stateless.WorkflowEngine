using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard
{
    public class VersionCheckResult
    {
        public bool IsNewVersionAvailable { get; set; }

        public string LatestReleseVersionNumber { get; set; }

    }
}
