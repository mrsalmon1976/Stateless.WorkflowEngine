using AutoMapper;
using Encryption;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Factories;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Common.Models;
using Stateless.WorkflowEngine.WebConsole.Common.Services;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Connection;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemWrapper.IO;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class DashboardModuleTest
    {
        private IVersionCheckService _versionCheckService;

        [SetUp]
        public void SetUp_DashboardModuleTest()
        {
            _versionCheckService = Substitute.For<IVersionCheckService>();

        }

        #region CheckForUpdate Tests

        [Test]
        public void CheckForUpdate_NotLoggedIn_GetsUnauthorizedResponse()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Get(Actions.Dashboard.CheckVersion, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        }

        [Test]
        public void CheckForUpdate_IsLoggedIn_ReturnsComparisonResult()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);

            VersionCheckResult checkResult = new VersionCheckResult();
            checkResult.LatestReleseVersionNumber = "1.2.3" ;
            _versionCheckService.CheckIfNewVersionAvailable().Returns(checkResult);

            // execute
            var response = browser.Get(Actions.Dashboard.CheckVersion, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            VersionCheckResult actionResult = JsonConvert.DeserializeObject<VersionCheckResult>(response.Body.AsString());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(checkResult.IsNewVersionAvailable, actionResult.IsNewVersionAvailable);
            Assert.AreEqual(checkResult.LatestReleseVersionNumber, actionResult.LatestReleseVersionNumber);
            _versionCheckService.Received(1).CheckIfNewVersionAvailable();
        }

        #endregion


        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new DashboardModule(_versionCheckService))
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    context.CurrentUser = currentUser;
                                })
                            );
            return browser;
        }

        #endregion


    }
}
