using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Configuration
{
    public interface IAppSettings
    {
        /// <summary>
        /// Gets the port used for the application.
        /// </summary>
        int Port { get; }
    }

    public class AppSettings : IAppSettings
    {
        /// <summary>
        /// Gets the port used for the application.
        /// </summary>
        public int Port 
        {
            get
            {
                return Int32.Parse(ConfigurationManager.AppSettings["Port"]);
            }
        }

    }
}
