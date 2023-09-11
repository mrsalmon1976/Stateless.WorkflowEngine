using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Nancy.Extensions;
using System.Collections.Generic;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System.IO;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using SystemWrapper.IO;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using AutoMapper;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.User;
using System.Diagnostics;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Nancy.Cryptography;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using Stateless.WorkflowEngine.WebConsole.Caching;
using Org.BouncyCastle.Asn1.CryptoPro;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;

namespace Stateless.WorkflowEngine.WebConsole
{

    public class WebConsoleBootstrapper : DefaultNancyBootstrapper
    {

        private static CryptographyConfiguration _cryptographyConfiguration;
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            // override maximum JSON length for Nancy
            Nancy.Json.JsonSettings.MaxJsonLength = int.MaxValue;

            // register settings first as we will use that below
            IAppSettings settings = new AppSettings();
            container.Register<IAppSettings>(settings);

            // set up the crypto config
            _cryptographyConfiguration = new CryptographyConfiguration(
                new RijndaelEncryptionProvider(new PassphraseKeyGenerator($"AES_{settings.SecureKey}", new byte[] { 101, 2, 103, 4, 105, 6, 107, 8 })),
                new DefaultHmacProvider(new PassphraseKeyGenerator($"HMAC_{settings.SecureKey}", new byte[] { 101, 2, 103, 4, 105, 6, 107, 8 })));

            // IO Wrappers
            container.Register<IDirectoryWrap, DirectoryWrap>();
            //container.Register<IPathWrap, PathWrap>();
            container.Register<IFileWrap, FileWrap>();
            //container.Register<IPathHelper, PathHelper>();

            // caching
            var memoryCache = new WebConsoleMemoryCache(new MemoryCacheOptions());
            container.Register<IMemoryCache>(memoryCache);
            container.Register<ICacheProvider, CacheProvider>();


            // security
            container.Register<Encryption.IEncryptionProvider, Encryption.AESGCM>();
            container.Register<IPasswordProvider, PasswordProvider>();

            // services
            container.Register<IGitHubVersionService, GitHubVersionService>();
            container.Register<IWebConsoleVersionService, WebConsoleVersionService>();
            container.Register<IVersionCheckService, VersionCheckService>();
            container.Register<IVersionUpdateService, VersionUpdateService>();
            container.Register<IVersionComparisonService>((c, o) => { return new VersionComparisonService(settings.LatestVersionUrl, container.Resolve<IWebConsoleVersionService>(), container.Resolve<IGitHubVersionService>()); });

            //container.Register<IBackgroundVersionWorker, BackgroundVersionWorker>();

            // set up mappings
            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<ConnectionViewModel, ConnectionModel>();
                cfg.CreateMap<ConnectionModel, ConnectionViewModel>();
                cfg.CreateMap<UserViewModel, UserModel>();
                //cfg.CreateMap<Workflow, UIWorkflow>();
            });
            var mapper = config.CreateMapper();
            container.Register<IMapper>(mapper);

            // set up the stores
            var dataPath = Path.Combine(this.RootPathProvider.GetRootPath(), "Data");
            var userStorePath = Path.Combine(dataPath, "users.json");
            
            IUserStore userStore = new UserStore(userStorePath, container.Resolve<IFileWrap>(), container.Resolve<IDirectoryWrap>(), container.Resolve<IPasswordProvider>());
            userStore.Load();
            container.Register<IUserStore>(userStore);

        }

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            base.ConfigureRequestContainer(container, context);
            
            // WebConsole classes and controllers
            container.Register<IUserMapper, UserMapper>();

        }

        protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);
            var formsAuthConfiguration = new FormsAuthenticationConfiguration()
            {
                CryptographyConfiguration = _cryptographyConfiguration,
                RedirectUrl = "~/login",
                UserMapper = container.Resolve<IUserMapper>(),
                DisableRedirect = context.Request.IsAjaxRequest()    
            };
            FormsAuthentication.Enable(pipelines, formsAuthConfiguration);

            // set shared ViewBag details here
            context.ViewBag.AppVersion = container.Resolve<IWebConsoleVersionService>().GetWebConsoleVersion();
            if (Debugger.IsAttached)
            {
                //context.ViewBag.AppVersion = DateTime.Now.ToString("yyyyMMddHHmmss");
            }
            context.ViewBag.Scripts = new List<string>();
            context.ViewBag.Claims = new List<string>();

            // before the request builds up, if there is a logged in user then set the user info
            pipelines.BeforeRequest += (ctx) =>
            {
                if (ctx.CurrentUser != null)
                {
                    ctx.ViewBag.CurrentUserName = ctx.CurrentUser.UserName;
                    if (ctx.CurrentUser.Claims != null)
                    {
                        ctx.ViewBag.Claims = new List<string>(ctx.CurrentUser.Claims);
                    }
                }
                return null;
            };
            pipelines.OnError.AddItemToEndOfPipeline((ctx, exc) =>
            {
                if (exc != null)
                {
                    _logger.Error(exc, exc.Message);
                    throw exc;
                }

                return HttpStatusCode.InternalServerError;
            });

            //// clean up anything that needs to be
            //pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            //    {
            //        IUnitOfWork uow = container.Resolve<IUnitOfWork>();
            //        uow.Dispose();
            //    });
        }

    }
}
