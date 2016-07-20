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
    public class UserModuleTest
    {
        private UserModule _userModule;
        private IUserStore _userStore;
        private IPasswordProvider _passwordProvider;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _passwordProvider = Substitute.For<IPasswordProvider>();
            _userModule = new UserModule(_userStore, _passwordProvider);
        }

        #region LoginPost Tests

        [TestCase("")]
        [TestCase("no")]
        public void ChangePaswordPost_InvalidPassword_ReturnsError(string password)
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.User.ChangePassword, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("Password", password);
                with.FormValue("ConfirmPassword", "ConfirmPasswordIsOk");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Length); 
        }

        [Test]
        public void ChangePaswordPost_PasswordDoesNotMatchConfirm_ReturnsError()
        {
            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            // execute
            var response = browser.Post(Actions.User.ChangePassword, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("Password", "IsValidPassword");
                with.FormValue("ConfirmPassword", "ButDoesNotMatch");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Length);
            Assert.IsTrue(result.Messages[0].Contains("do not match"));
        }

        [Test]
        public void ChangePaswordPost_PasswordValid_UpdatesAndSaves()
        {
            const string newPassword = "IsValidPassword";
            const string oldPassword = "blahblahblah";
            string salt = Guid.NewGuid().ToString();
            string newHashedPassword = Guid.NewGuid().ToString();

            // setup
            var bootstrapper = this.ConfigureBootstrapper();
            var browser = new Browser(bootstrapper);

            UserModel user = new UserModel()
            {
                Id = bootstrapper.CurrentUser.Id,
                UserName = bootstrapper.CurrentUser.UserName,
                Role = bootstrapper.CurrentUser.Claims.First(),
                Password = oldPassword
            };
            List<UserModel> users = new List<UserModel>() { user };
            _userStore.Users.Returns(users);

            _passwordProvider.GenerateSalt().Returns(salt);
            _passwordProvider.HashPassword(newPassword, salt).Returns(newHashedPassword);

            // execute
            var response = browser.Post(Actions.User.ChangePassword, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(bootstrapper.CurrentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("Password", newPassword);
                with.FormValue("ConfirmPassword", newPassword);
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Length);

            // make sure the user was updated and saved
            Assert.AreEqual(newHashedPassword, user.Password);
            _userStore.Received(1).Save();
            _passwordProvider.Received(1).GenerateSalt();
            _passwordProvider.Received(1).HashPassword(newPassword, salt);
        }

        #endregion

        #region Private Methods

        private ModuleTestBootstrapper ConfigureBootstrapper()
        {
            var bootstrapper = new ModuleTestBootstrapper();
            bootstrapper.Login();
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
