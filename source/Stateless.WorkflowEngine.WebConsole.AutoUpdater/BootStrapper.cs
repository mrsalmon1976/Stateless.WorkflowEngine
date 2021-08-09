using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Update;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Version;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.BLL.Web;
using Stateless.WorkflowEngine.WebConsole.AutoUpdater.Services.Windows;
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

            container.Register<IAssemblyVersionChecker, AssemblyVersionChecker>(Lifestyle.Transient);
            container.Register<IFileUtility, FileUtility>(Lifestyle.Transient);
            container.Register<IInstallationService, InstallationService>(Lifestyle.Transient);
            container.Register<IUpdateDownloadService, UpdateDownloadService>(Lifestyle.Transient);
            container.Register<IUpdateFileService, UpdateFileService>(Lifestyle.Transient);
            container.Register<IUpdateLocationService, UpdateLocationService>(Lifestyle.Transient);
            container.Register<IVersionComparisonService, VersionComparisonService>(Lifestyle.Transient);
            container.Register<IWebVersionChecker, WebVersionChecker>(Lifestyle.Transient);

            container.Register<UpdateOrchestrator>();

            container.Verify();
            return container;
        }
    }
}
