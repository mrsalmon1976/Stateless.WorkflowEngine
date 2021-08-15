using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Web;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Utility;

namespace Stateless.WorkflowEngine.WebConsole.AutoUpdater
{
    public class BootStrapper
    {
        public static Container Boot()
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            container.Register<HttpClient>(() => new HttpClient(), Lifestyle.Scoped);

            // singletons
            container.Register<IAppSettings, AppSettings>(Lifestyle.Singleton);
            container.Register<IHttpClientFactory, HttpClientFactory>(Lifestyle.Singleton);

            container.Register<IAssemblyVersionService, AssemblyVersionService>(Lifestyle.Transient);
            container.Register<IFileUtility, FileUtility>(Lifestyle.Transient);
            container.Register<IInstallationService, InstallationService>(Lifestyle.Transient);
            container.Register<IUpdateDownloadService, UpdateDownloadService>(Lifestyle.Transient);
            container.Register<IUpdateEventLogger, UpdateEventLogger>(Lifestyle.Transient);
            container.Register<IUpdateFileService, UpdateFileService>(Lifestyle.Transient);
            container.Register<IUpdateLocationService, UpdateLocationService>(Lifestyle.Transient);
            container.Register<IVersionComparisonService, VersionComparisonService>(Lifestyle.Transient);
            container.Register<IWebVersionService, WebVersionService>(Lifestyle.Transient);

            container.Register<UpdateOrchestrator>();

            container.Verify();
            return container;
        }
    }
}
