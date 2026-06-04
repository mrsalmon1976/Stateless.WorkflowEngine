using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Caching
{
    public class CacheKeys
    {
        public const string CheckIfNewVersionAvailable = "CheckIfNewVersionAvailable";

        public static string ConnectionInfo(string connectionId)
        {
            return $"ConnectionInfo_{connectionId}";
        }

        public static string CustomDashboardInfo(string dashboardId, string connectionId)
        {
            return $"CustomDashboardInfo_{dashboardId}_{connectionId}";
        }
    }
}
