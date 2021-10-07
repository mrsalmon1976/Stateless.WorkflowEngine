using Nancy;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.BLL.Services;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Dashboard;
using System;
using System.Collections.Generic;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class UpdateModuleTest
    {
        private IVersionUpdateService _versionUpdateService;
        private IVersionCheckService _versionCheckService;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _versionUpdateService = Substitute.For<IVersionUpdateService>();
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
            var response = browser.Get(Actions.Update.CheckForUpdate, (with) =>
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
            checkResult.LatestReleaseVersionNumber = "1.2.3";
            _versionCheckService.CheckIfNewVersionAvailable().Returns(checkResult);

            // execute
            var response = browser.Get(Actions.Update.CheckForUpdate, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            VersionCheckResult actionResult = JsonConvert.DeserializeObject<VersionCheckResult>(response.Body.AsString());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(checkResult.IsNewVersionAvailable, actionResult.IsNewVersionAvailable);
            Assert.AreEqual(checkResult.LatestReleaseVersionNumber, actionResult.LatestReleaseVersionNumber);
            _versionCheckService.Received(1).CheckIfNewVersionAvailable();
        }

        #endregion
        #region Install Tests

        [Test]
        public void Install_NotLoggedIn_ThrowsNotAuth()
        {
            // setup
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Get(Actions.Update.Install, (with) =>
            {
                with.HttpRequest();
            });
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Test]
        public void Install_LoggedIn_KicksOffInstallationProcess()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);

            // execute
            var response = browser.Get(Actions.Update.Install, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            Assert.AreEqual(HttpStatusCode.SeeOther, response.StatusCode);
            Assert.AreEqual(Actions.Update.Index, response.Headers["Location"]);

            _versionUpdateService.Received(1).InstallUpdate();
        }


        #endregion


        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new UpdateModule(_versionUpdateService, _versionCheckService))
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    context.CurrentUser = currentUser;
                                    context.ViewBag.Scripts = new List<string>();
                                    context.ViewBag.Claims = new List<string>();
                                    context.CurrentUser = currentUser;
                                    if (currentUser != null)
                                    {
                                        context.ViewBag.CurrentUserName = currentUser?.UserName;
                                    }
                                })
                            );
            return browser;
        }

        private List<UserModel> ConfigureUsers(UserIdentity currentUser, string[] claims)
        {
            // set up the logged in user
            UserModel user = new UserModel()
            {
                Id = currentUser.Id,
                UserName = currentUser.UserName,
                Role = Roles.User,
                Claims = claims
            };
            List<UserModel> users = new List<UserModel>() { user };
            //_userStore.Users.Returns(users);
            return users;
        }

        #endregion


    }
}
