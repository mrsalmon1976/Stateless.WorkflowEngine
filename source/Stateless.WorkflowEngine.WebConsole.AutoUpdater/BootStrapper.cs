using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Stateless.WorkflowEngine.WebConsole.Common.Web;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Logging;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Utility;
using Stateless.WorkflowEngine.WebConsole.Common.Services;

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
            IAppSettings appSettings = new AppSettings();
            container.RegisterInstance<IAppSettings>(appSettings);
            container.Register<IHttpClientFactory, HttpClientFactory>(Lifestyle.Singleton);

            container.Register<IFileUtility, FileUtility>(Lifestyle.Transient);
            container.Register<IInstallationService, InstallationService>(Lifestyle.Transient);
            container.Register<IUpdateDownloadService, UpdateDownloadService>(Lifestyle.Transient);
            container.Register<IUpdateEventLogger, UpdateEventLogger>(Lifestyle.Transient);
            container.Register<IUpdateFileService, UpdateFileService>(Lifestyle.Transient);
            container.Register<IUpdateLocationService, UpdateLocationService>(Lifestyle.Transient);
            container.Register<IWebVersionService, WebVersionService>(Lifestyle.Transient);

            container.Register<IWebConsoleVersionService>(() => { return new AssemblyVersionService(AutoUpdaterConstants.WebConsoleExeFileName, container.GetInstance<IUpdateLocationService>()); }, Lifestyle.Transient);
            container.Register<IVersionComparisonService>(() => { return new VersionComparisonService(appSettings.LatestVersionUrl, container.GetInstance<IWebConsoleVersionService>(), container.GetInstance<IWebVersionService>()); }, Lifestyle.Transient);

            container.Register<UpdateOrchestrator>();


            container.Verify();
            return container;
        }
    }
}
