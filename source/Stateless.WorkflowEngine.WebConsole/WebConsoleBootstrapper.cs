using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Nancy.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Linq;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using System.IO;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using SystemWrapper.IO;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Encryption;
using AutoMapper;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.User;
using System.Diagnostics;

namespace Stateless.WorkflowEngine.WebConsole
{

    public class WebConsoleBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            // override maximum JSON length for Nancy
            Nancy.Json.JsonSettings.MaxJsonLength = int.MaxValue;

            // register settings first as we will use that below
            IAppSettings settings = new AppSettings();
            container.Register<IAppSettings>(settings);

            // IO Wrappers
            container.Register<IDirectoryWrap, DirectoryWrap>();
            //container.Register<IPathWrap, PathWrap>();
            container.Register<IFileWrap, FileWrap>();
            //container.Register<IPathHelper, PathHelper>();

            // security
            container.Register<IEncryptionProvider, AESGCM>();
            container.Register<IPasswordProvider, PasswordProvider>();

            // set up mappings
            Mapper.Initialize((cfg) => {
                cfg.CreateMap<ConnectionModel, WorkflowStoreModel>();//.ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.DocumentId));
                cfg.CreateMap<UserViewModel, UserModel>();//.ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.DocumentId));
            });

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
            
            IAppSettings settings = container.Resolve<IAppSettings>();

            // WebConsole classes and controllers
            container.Register<IUserMapper, UserMapper>();

        }

        protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);
            var formsAuthConfiguration = new FormsAuthenticationConfiguration()
            {
                RedirectUrl = "~/login",
                UserMapper = container.Resolve<IUserMapper>(),
                DisableRedirect = context.Request.IsAjaxRequest()    
            };
            FormsAuthentication.Enable(pipelines, formsAuthConfiguration);

            // set shared ViewBag details here
            context.ViewBag.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            if (Debugger.IsAttached)
            {
                context.ViewBag.AppVersion = DateTime.Now.ToString("yyyyMMddHHmmssttt");
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

            //// clean up anything that needs to be
            //pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            //    {
            //        IUnitOfWork uow = container.Resolve<IUnitOfWork>();
            //        uow.Dispose();
            //    });
        }

    }
}
