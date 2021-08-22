using Stateless.WorkflowEngine.WebConsole.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Common.Models
{
    public class VersionComparisonResult
    {
        public bool IsNewVersionAvailable { get; set; }

        public WebConsoleVersionInfo LatestReleaseVersionInfo { get; set; }
    }
}
