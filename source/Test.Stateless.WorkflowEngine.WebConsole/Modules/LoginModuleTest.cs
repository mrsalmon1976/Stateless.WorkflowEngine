using Nancy;
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
        private LoginModule _loginModule;
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;

        [SetUp]
        public void LoginModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _userStore.Users.Returns(new List<UserModel>());
            _passwordProvider = Substitute.For<IPasswordProvider>();

            _loginModule = new LoginModule(_userStore, _passwordProvider);
        }

        #region LoginGet Tests

        [Test]
        public void LoginGet_UserLoggedIn_RedirectsToDashboard()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            bootstrapper.Login();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            response.ShouldHaveRedirectedTo(Actions.Dashboard.Default);
        }

        [Test]
        public void LoginGet_NoReturnUrl_DefaultsToDashboard()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper); 

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                //with.FormsAuth(Guid.NewGuid(), new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            response.Body["#returnUrl"]
                .ShouldExistOnce()
                .And.ShouldContainAttribute("value", Actions.Dashboard.Default);
        }

        [Test]
        public void LoginGet_WithReturnUrl_SetsReturnUrlFormValue()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Get(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.Query("returnUrl", "/test");
                //with.FormsAuth(Guid.NewGuid(), new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
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
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("Password", "password");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void LoginPost_NoPassword_LoginFailsWithoutCheck()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void LoginPost_UserNotFound_LoginFails()
        {
            // setup
            bool userStoreChecked = false;
            _userStore.Users.Returns(new List<UserModel>()).AndDoes((c) => { userStoreChecked = true; });
            var bootstrapper = this.ConfigureBootstrapper();

            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _passwordProvider.DidNotReceive().CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.IsTrue(userStoreChecked);
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

            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            _passwordProvider.Received(1).CheckPassword(Arg.Any<string>(), Arg.Any<string>());

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.IsTrue(userStoreChecked);
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

            var bootstrapper = this.ConfigureBootstrapper();
            bootstrapper.ConfigureRequestStartupCallback = (container, pipelines, context) =>
            {
                var formsAuthConfiguration = new FormsAuthenticationConfiguration()
                {
                    RedirectUrl = "~/login",
                    UserMapper = container.Resolve<IUserMapper>(),
                };
                FormsAuthentication.Enable(pipelines, formsAuthConfiguration);
            };
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.Login.Default, (with) =>
            {
                with.HttpRequest();
                with.FormValue("UserName", "admin");
                with.FormValue("Password", "password");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.SeeOther, response.StatusCode);
            Assert.IsNotNullOrEmpty(response.Headers["Location"]);
            _passwordProvider.Received(1).CheckPassword(Arg.Any<string>(), Arg.Any<string>());
            Assert.IsTrue(userStoreChecked);
            Assert.IsNullOrEmpty(response.Body.AsString());
        }

        #endregion

        #region Private Methods

        private ModuleTestBootstrapper ConfigureBootstrapper()
        {
            var bootstrapper = new ModuleTestBootstrapper();
            bootstrapper.ConfigureRequestContainerCallback = (container) =>
            {
                container.Register<IUserStore>(_userStore);
                container.Register<IPasswordProvider>(_passwordProvider);
            };
            return bootstrapper;
        }

        #endregion


    }
}
