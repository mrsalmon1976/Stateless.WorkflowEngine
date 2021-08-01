using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    public interface IAppSettings
    {
        string LatestVersionUrl { get;  }
    }
    public class AppSettings : IAppSettings
    {
        public string LatestVersionUrl { get { return ConfigurationManager.AppSettings["LatestVersionUrl"]; }  }
    }
}
