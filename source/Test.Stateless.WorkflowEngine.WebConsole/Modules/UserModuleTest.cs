using AutoMapper;
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
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using Stateless.WorkflowEngine.WebConsole.Modules;
using Stateless.WorkflowEngine.WebConsole.Navigation;
using Stateless.WorkflowEngine.WebConsole.ViewModels;
using Stateless.WorkflowEngine.WebConsole.ViewModels.Login;
using Stateless.WorkflowEngine.WebConsole.ViewModels.User;
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
        private IUserStore _userStore;
        private IUserValidator _userValidator;
        private IPasswordProvider _passwordProvider;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _userValidator = Substitute.For<IUserValidator>();
            _passwordProvider = Substitute.For<IPasswordProvider>();

            Mapper.Initialize((cfg) =>
            {
                cfg.CreateMap<UserViewModel, UserModel>();
            });

        }

        #region ChangePassword Tests

        [TestCase("")]
        [TestCase("no")]
        public void ChangePassword_InvalidPassword_ReturnsError(string password)
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            // execute
            var response = browser.Post(Actions.User.ChangePassword, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
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
        public void ChangePassword_PasswordDoesNotMatchConfirm_ReturnsError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            // execute
            var response = browser.Post(Actions.User.ChangePassword, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
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
        public void ChangePassword_PasswordValid_UpdatesAndSaves()
        {
            const string newPassword = "IsValidPassword";
            const string oldPassword = "blahblahblah";
            string salt = Guid.NewGuid().ToString();
            string newHashedPassword = Guid.NewGuid().ToString();

            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            UserModel user = new UserModel()
            {
                Id = currentUser.Id,
                UserName = currentUser.UserName,
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
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
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

        #region Save Tests

        [Test]
        public void Save_AuthTest()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            _userStore.Users.Returns(new List<UserModel>() { });
            _userValidator.Validate(Arg.Any<UserModel>()).Returns(new ValidationResult());

            foreach (string claim in Claims.AllClaims)
            {

                currentUser.Claims = new string[] { claim };

                // execute
                var response = browser.Post(Actions.User.Save, (with) =>
                {
                    with.HttpRequest();
                    with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                    //with.FormValue("id", connectionId.ToString());
                });

                // assert
                if (claim == Claims.UserAdd)
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }

        }

        [Test]
        public void Save_InvalidUser_ReturnsFailure()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.UserAdd };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            _userValidator.Validate(Arg.Any<UserModel>()).Returns(new ValidationResult("error"));

            // execute
            var response = browser.Post(Actions.User.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("Password", "password");
                with.FormValue("ConfirmPassword", "password");
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Length);
            _userStore.DidNotReceive().Save();

        }

        [Test]
        public void Save_PasswordsDoNotMatch_ReturnsFailure()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.UserAdd };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );

            _userValidator.Validate(Arg.Any<UserModel>()).Returns(new ValidationResult());

            // execute
            var response = browser.Post(Actions.User.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("ConfirmPassword", Guid.NewGuid().ToString());
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.Messages.Length);
            _userStore.DidNotReceive().Save();

        }

        [Test]
        public void Save_ValidUser_Saves()
        {
            // setup
            const string userName = "TestUser";
            const string password = "password1";

            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.UserAdd };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );
            _userStore.Users.Returns(new List<UserModel>());

            _userValidator.Validate(Arg.Any<UserModel>()).Returns(new ValidationResult());

            // execute
            var response = browser.Post(Actions.User.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("UserName", userName);
                with.FormValue("Password", password);
                with.FormValue("ConfirmPassword", password);
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Length);
            _userStore.Received(1).Save();

            // the user should have been added
            List<UserModel> users = _userStore.Users;
            Assert.AreEqual(1, users.Count);
            Assert.AreEqual(userName, users[0].UserName);
        }

        [Test]
        public void Save_ValidUser_PasswordHashed()
        {
            // setup
            const string userName = "TestUser";
            const string password = "password1";

            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.UserAdd };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_userStore, _userValidator, _passwordProvider))
                    .RequestStartup((container, pipelines, context) => {
                        context.CurrentUser = currentUser;
                    })
                );
            _userStore.Users.Returns(new List<UserModel>());

            _userValidator.Validate(Arg.Any<UserModel>()).Returns(new ValidationResult());

            string salt = Guid.NewGuid().ToString();
            string hashedPassword = Guid.NewGuid().ToString();
            _passwordProvider.GenerateSalt().Returns(salt);
            _passwordProvider.HashPassword(password, salt).Returns(hashedPassword);

            // execute
            var response = browser.Post(Actions.User.Save, (with) =>
            {
                with.HttpRequest();
                with.FormsAuth(currentUser.Id, new Nancy.Authentication.Forms.FormsAuthenticationConfiguration());
                with.FormValue("UserName", userName);
                with.FormValue("Password", password);
                with.FormValue("ConfirmPassword", password);
            });

            // assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Messages.Length);

            _passwordProvider.Received(1).GenerateSalt();
            _passwordProvider.Received(1).HashPassword(password, salt);

            List<UserModel> users = _userStore.Users;
            Assert.AreEqual(hashedPassword, users[0].Password);
        }

        #endregion



    }
}
