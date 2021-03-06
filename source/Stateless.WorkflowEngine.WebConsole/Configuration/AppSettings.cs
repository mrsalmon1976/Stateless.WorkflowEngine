﻿using System;
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

        /// <summary>
        /// Gets/sets the application security key
        /// </summary>
        string SecureKey { get; }
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

        /// <summary>
        /// Gets/sets the application security key
        /// </summary>
        public string SecureKey
        {
            get
            {
                return (ConfigurationManager.AppSettings["SecureKey"] ?? "this a Def@ult security key if it is not specified in app.config");
            }
        }

    }
}
