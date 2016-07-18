using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
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

namespace Stateless.WorkflowEngine.WebConsole
{

    public class WebConsoleBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            // register settings first as we will use that below
            IAppSettings settings = new AppSettings();
            container.Register<IAppSettings>(settings);

            // IO Wrappers
            //container.Register<IDirectoryWrap, DirectoryWrap>();
            //container.Register<IPathWrap, PathWrap>();
            container.Register<IFileWrap, FileWrap>();
            //container.Register<IPathHelper, PathHelper>();

            // security
            container.Register<IPasswordProvider, PasswordProvider>();

            //// set up mappings
            //Mapper.Initialize((cfg) => {
            //    cfg.CreateMap<DocumentViewModel, DocumentEntity>().ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.DocumentId));
            //    cfg.CreateMap<DocumentEntity, DocumentViewModel>().ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.Id));
            //});

            // set up the stores
            var dataPath = Path.Combine(this.RootPathProvider.GetRootPath(), "Data");
            var userStorePath = Path.Combine(dataPath, "users.json");
            
            IUserStore userStore = new UserStore(userStorePath, container.Resolve<IFileWrap>(), container.Resolve<IPasswordProvider>());
            userStore.Load();
            container.Register<IUserStore>(userStore);

            //// at this point, run in any database changes if there are any
            //using (IDbConnection conn = new SqlConnection(settings.ConnectionString))
            //{
            //    IDbScriptResourceProvider resourceProvider = container.Resolve<IDbScriptResourceProvider>();
            //    Console.WriteLine("Running migrations...");
            //    new DbMigrator().Migrate(conn, settings.DbSchema, resourceProvider.GetDbMigrationScripts());
            //    Console.WriteLine("...Done.");
            //}


        }

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            base.ConfigureRequestContainer(container, context);
            
            IAppSettings settings = container.Resolve<IAppSettings>();

            //// set up a new DB Connection per request
            //IDbConnection conn = new SqlConnection();
            //conn.ConnectionString = settings.ConnectionString;
            //conn.Open();
            //container.Register<IDbConnection>(conn);

            // BLL repositories
            //container.Register<IAuditLogRepository>(new AuditLogRepository(conn, settings.DbSchema));
            //container.Register<ICategoryRepository>(new CategoryRepository(conn, settings.DbSchema));
            //container.Register<IDocumentRepository>(new DocumentRepository(conn, settings.DbSchema));
            //container.Register<IDocumentCategoryAsscRepository>(new DocumentCategoryAsscRepository(conn, settings.DbSchema));
            //container.Register<IDocumentVersionRepository>(new DocumentVersionRepository(conn, settings.DbSchema));
            //container.Register<IUserRepository>(new UserRepository(conn, settings.DbSchema));
            //container.Register<IUserCategoryAsscRepository>(new UserCategoryAsscRepository(conn, settings.DbSchema));

            //// set up the unit of work which will be used for database access
            //IUnitOfWork unitOfWork = new UnitOfWork(conn, settings.DbSchema
            //    , container.Resolve<IAuditLogRepository>()
            //    , container.Resolve<ICategoryRepository>()
            //    , container.Resolve<IDocumentRepository>()
            //    , container.Resolve<IDocumentCategoryAsscRepository>()
            //    , container.Resolve<IDocumentVersionRepository>()
            //    , container.Resolve<IUserRepository>()
            //    , container.Resolve<IUserCategoryAsscRepository>()
            //    );
            //container.Register<IUnitOfWork>(unitOfWork);

            // WebConsole classes and controllers
            container.Register<IUserMapper, UserMapper>();

            //// BLL commands
            //container.Register<ISaveAuditLogCommand, SaveAuditLogCommand>();
            //container.Register<ISaveCategoryCommand, SaveCategoryCommand>();
            //container.Register<ISaveDocumentCommand, SaveDocumentCommand>();
            //container.Register<ISaveUserCommand, SaveUserCommand>();

            // other BLL classes
            //container.Register<IAuditLogValidator, AuditLogValidator>();
            //container.Register<ICategoryValidator, CategoryValidator>();
            //container.Register<IDocumentValidator, DocumentValidator>();
            //container.Register<IUserValidator, UserValidator>();

        }

        protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);

            var formsAuthConfiguration = new FormsAuthenticationConfiguration()
            {
                RedirectUrl = "~/login",
                UserMapper = container.Resolve<IUserMapper>(),
            };
            FormsAuthentication.Enable(pipelines, formsAuthConfiguration);

            // set shared ViewBag details here
            context.ViewBag.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            context.ViewBag.Scripts = new List<string>();
            //context.ViewBag.IsAdmin = false;

            //// before the request builds up, if there is a logged in user then set the admin info
            //pipelines.BeforeRequest += (ctx) =>
            //{
            //    if (ctx.CurrentUser != null)
            //    {
            //        ctx.ViewBag.IsAdmin = ctx.CurrentUser.Claims.Contains(Roles.Admin);
            //    }
            //    return null;
            //};

            //// clean up anything that needs to be
            //pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            //    {
            //        IUnitOfWork uow = container.Resolve<IUnitOfWork>();
            //        uow.Dispose();
            //    });
        }

    }
}
