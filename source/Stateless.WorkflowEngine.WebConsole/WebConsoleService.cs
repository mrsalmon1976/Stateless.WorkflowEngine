using System;
using Nancy.Hosting.Self;
using NLog;
using Stateless.WorkflowEngine.WebConsole.Configuration;

namespace Stateless.WorkflowEngine.WebConsole
{
    public class WebConsoleService
    {
        private NancyHost _host;
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
        }

        public void Stop()
        {
            try
            {
                _logger.Info("Stateless.WorkflowEngine Windows Service shutting down");
                _host.Stop();
                _host.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }
    }
}
