using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater.Models
{
    public class VersionComparisonResult
    {
        public bool IsNewVersionAvailable { get; set; }

        public WebConsoleVersionInfo LatestReleaseVersionInfo { get; set; }
    }
}
