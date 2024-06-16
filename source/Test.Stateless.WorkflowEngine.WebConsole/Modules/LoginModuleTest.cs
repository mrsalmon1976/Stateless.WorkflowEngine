﻿using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Bootstrapper;
using Nancy.Responses.Negotiation;
using Nancy.Testing;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.Modules
{
    [TestFixture]
    public class LoginModuleTest
    {
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;

        [SetUp]
        public void LoginModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _userStore.Users.Returns(new List<UserModel>());
            _passwordProvider = Substitute.For<IPasswordProvider>();
        }

        #region LoginGet Tests

        [Test]
        public void LoginGet_UserLoggedIn_RedirectsToDashboard()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = CreateBrowser(currentUser);

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            response.ShouldHaveRedirectedTo(Actions.Dashboard.Default);
        }

        [Test]
        public void LoginGet_NoReturnUrl_DefaultsToDashboard()
        {
            // setup
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                //with.FormsAuth(Guid.NewGuid(), new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            response.Body["#returnUrl"]
                .ShouldExistOnce()
                .And.ShouldContainAttribute("value", Actions.Dashboard.Default);
        }

        [Test]
        public void LoginGet_WithReturnUrl_SetsReturnUrlFormValue()
        {
            // setup
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.Query("returnUrl", "/test");
                //with.FormsAuth(Guid.NewGuid(), new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            response.Body["#returnUrl"]
                .ShouldExistOnce()
                .And.ShouldContainAttribute("value", "/test");
        }

        #endregion

        #region LoginPost Tests

        [Test]
        public void LoginPost_NoUserName_LoginFailsWithoutCheck()
        {
            // setup
            var browser = CreateBrowser(null);

            //var browser = new Browser(with => with.Module(new LoginModule(_userStore, _passwordProvider)));

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "password");
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void LoginPost_NoPassword_LoginFailsWithoutCheck()
        {
            // setup
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void LoginPost_UserNotFound_LoginFails()
        {
            // setup
            bool userStoreChecked = false;
            _userStore.Users.Returns(new List<UserModel>()).AndDoes((c) => { userStoreChecked = true; });
            var browser = CreateBrowser(null);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(userStoreChecked, Is.True);
        }

        [Test]
        public void LoginPost_UserFoundButPasswordIncorrect_LoginFails()
        {
            // setup
            bool userStoreChecked = false;
            List<UserModel> users = new List<UserModel>();
            users.Add(new UserModel()
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                Password = "dsdsdds"
            });
            _userStore.Users.Returns(users).AndDoes((c) => { userStoreChecked = true; });

            _passwordProvider.CheckPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            var browser = CreateBrowser(null);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            _passwordProvider.Received(1).CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(userStoreChecked, Is.True);
        }

        [Test]
        public void LoginPost_ValidLogin_LoginSucceeds()
        {
            // setup
            bool userStoreChecked = false;
            List<UserModel> users = new List<UserModel>();
            users.Add(new UserModel()
            {
                Id = Guid.NewGuid(),
                UserName = "admin",
                Password = "password"
            });
            _userStore.Users.Returns(users).AndDoes((c) => { userStoreChecked = true; });

            _passwordProvider.CheckPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);


            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new LoginModule(_userStore, _passwordProvider))
                                .RootPathProvider(new TestRootPathProvider())
                                .RequestStartup((container, pipelines, context) => {
                                    container.Register<IUserMapper, UserMapper>();
                                    container.Register<IUserStore>(Substitute.For<IUserStore>());
                                    var formsAuthConfiguration = new FormsAuthenticationConfiguration()
                                    {
                                        RedirectUrl = "~/login",
                                        UserMapper = container.Resolve<IUserMapper>(),
                                    };
                                    FormsAuthentication.Enable(pipelines, formsAuthConfiguration);
                                })
                            );

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.SeeOther));
            Assert.That(response.Headers["Location"], Is.Not.Null);
            Assert.That(response.Headers["Location"], Is.Not.Empty);
            _passwordProvider.Received(1).CheckPassword(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(userStoreChecked, Is.True);
            Assert.That(response.Body.AsString(), Is.Empty);
        }

        #endregion

        #region Private Methods

        private Browser CreateBrowser(UserIdentity currentUser)
        {
            var browser = new Browser((bootstrapper) =>
                            bootstrapper.Module(new LoginModule(_userStore, _passwordProvider))
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
