using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace Stateless.WorkflowEngine.WebConsole
{
    class Program
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            _logger.Info("Stateless.WorkflowEngine Console starting up");

            try
            {
                HostFactory.Run(
                    configuration =>
                    {
                        configuration.Service<WebConsoleService>(
                            service =>
                            {
                                service.ConstructUsing(x => new WebConsoleService());
                                service.WhenStarted(x => x.Start());
                                service.WhenStopped(x => x.Stop());
                            });

                        configuration.RunAsLocalSystem();

                        configuration.SetServiceName("Stateless.WorkflowEngine.Console");
                        configuration.SetDisplayName("Stateless.WorkflowEngine Console");
                        configuration.SetDescription("The Stateless.WorkflowEngine Console service.");
                    });
                _logger.Info("Stateless.WorkflowEngine Console shutting down");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Stateless.WorkflowEngine Console crashed!");
            }


        }
    }
}
