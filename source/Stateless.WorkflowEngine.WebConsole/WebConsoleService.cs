using System;
using System.IO;
using Nancy.Hosting.Self;
using NLog;
using Stateless.WorkflowEngine.WebConsole.Common;
using Stateless.WorkflowEngine.WebConsole.Common.Utility;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.Jobs;

namespace Stateless.WorkflowEngine.WebConsole
{
    public class WebConsoleService
    {
        private NancyHost _host;
        private AutoUpdaterCleanupJob _autoUpdaterCleanupJob;

        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public void Start()
        {
            _logger.Info("Stateless.WorkflowEngine Windows Service starting");

            IAppSettings appSettings = new AppSettings();

            var hostConfiguration = new HostConfiguration
            {
                UrlReservations = new UrlReservations() { CreateAutomatically = true }
            };

            string url = String.Format("http://localhost:{0}", appSettings.Port);
            _host = new NancyHost(hostConfiguration, new Uri(url));
            _host.Start();

            _autoUpdaterCleanupJob = new AutoUpdaterCleanupJob(AppDomain.CurrentDomain.BaseDirectory, new FileUtility());
            _autoUpdaterCleanupJob.Start();
        }

        public void Stop()
        {
            try
            {
                _logger.Info("Stateless.WorkflowEngine Windows Service shutting down");

                if (_autoUpdaterCleanupJob != null)
                {
                    _autoUpdaterCleanupJob.Dispose();
                }

                if (_host != null)
                {
                    _host.Stop();
                    _host.Dispose();
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }
    }
}
