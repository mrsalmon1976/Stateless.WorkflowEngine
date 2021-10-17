using SimpleInjector;
using SimpleInjector.Lifestyles;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = BootStrapper.Boot();
            if (Debugger.IsAttached)
            {
                Console.WriteLine("DEBUG MODE: Enter the path where the installation exists: ");
                string installationPath = Console.ReadLine();
                container.GetInstance<IUpdateLocationService>().ApplicationFolder = installationPath;
            }

            IUpdateEventLogger logger = container.GetInstance<IUpdateEventLogger>();
            logger.ClearLogFile();
            logger.LogLine($"Auto Update start: {DateTime.Now}");
            try
            {
                using (Scope scope = AsyncScopedLifestyle.BeginScope(container))
                {
                    UpdateOrchestrator orchestrator = container.GetInstance<UpdateOrchestrator>();
                    orchestrator.Run().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                logger.LogLine($"ERROR: {ex.Message}");
                logger.LogLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
            logger.LogLine($"Auto Update end: {DateTime.Now}");
        }
    }
}
