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
using NSubstitute;
using System.Security.Principal;
using Nancy.Security;
using Nancy.ViewEngines.Razor;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{

    /// <summary>
    /// Bootstrapper for module unit tests.
    /// </summary>
    public class ModuleTestBootstrapper : DefaultNancyBootstrapper
    {
        private IRootPathProvider _rootPathProvider;

        public ModuleTestBootstrapper()
        {
            _rootPathProvider = new TestRootPathProvider();
        }

        public Action<TinyIoCContainer> ApplicationStartupCallback { get; set; }

        public Action<TinyIoCContainer> ConfigureRequestContainerCallback { get; set; }

        public Action<TinyIoCContainer, IPipelines, NancyContext> ConfigureRequestStartupCallback { get; set; }

        protected override IRootPathProvider RootPathProvider
        {
            get
            {
                return _rootPathProvider;
            }
        }
        /// <summary>
        /// Gets/sets the current user - set this to null if you want to simulate no auth.
        /// </summary>
        public UserIdentity CurrentUser { get; set; }

        /// <summary>
        /// Simulates a login and returns the user created.
        /// </summary>
        /// <returns></returns>
        public UserIdentity Login()
        {
            this.CurrentUser = new UserIdentity()
            {
                Id = Guid.NewGuid(),
                UserName = "Joe Soap",
                Claims = new string[] { Roles.Admin }
            };
            return this.CurrentUser;
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);
            //container.Register<IUserStore>(Substitute.For<IUserStore>());
            if (this.ApplicationStartupCallback != null)
            {
                this.ApplicationStartupCallback(container);
            }
        }

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            base.ConfigureRequestContainer(container, context);
            container.Register<IUserMapper, UserMapper>();
            if (this.ConfigureRequestContainerCallback != null)
            {
                this.ConfigureRequestContainerCallback(container);
            }
            
        }

        protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);

            if (this.ConfigureRequestStartupCallback != null)
            {
                this.ConfigureRequestStartupCallback(container, pipelines, context);
            }
            //var formsAuthConfiguration = new FormsAuthenticationConfiguration()
            //{
            //    RedirectUrl = "~/login",
            //    UserMapper = container.Resolve<IUserMapper>(),
            //};
            //FormsAuthentication.Enable(pipelines, formsAuthConfiguration);

            context.ViewBag.Scripts = new List<string>();
            context.CurrentUser = this.CurrentUser;

        }

    }

    /// <summary>
    /// Required for Razor configuration.
    /// </summary>
    public class RazorConfig : IRazorConfiguration
    {
        public IEnumerable<string> GetAssemblyNames()
        {
            return null;
        }

        public IEnumerable<string> GetDefaultNamespaces()
        {
            return null;
        }

        public bool AutoIncludeModelNamespace
        {
            get { return false; }
        }
    }

    /// <summary>
    /// Override the root path provider for testing so Nancy can locate the views.
    /// </summary>
    public class TestRootPathProvider : IRootPathProvider
    {
        public string GetRootPath()
        {
            return Environment.CurrentDirectory;
        }
    }

}
