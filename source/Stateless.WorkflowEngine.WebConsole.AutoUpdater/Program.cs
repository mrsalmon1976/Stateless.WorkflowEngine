using NLog;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    class Program
    {
        private static ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            _logger.Info("Auto Update start");
            try
            {
                var container = BootStrapper.Boot();
                using (Scope scope = AsyncScopedLifestyle.BeginScope(container))
                {
                    UpdateOrchestrator orchestrator = container.GetInstance<UpdateOrchestrator>();
                    orchestrator.Run().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _logger.Error(ex, ex.Message);
            }
            _logger.Info("Auto Update end");
        }
    }
}
