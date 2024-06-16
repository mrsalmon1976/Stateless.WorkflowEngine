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
        private IMapper _mapper;
        private IUserStore _userStore;
        private IUserValidator _userValidator;
        private IPasswordProvider _passwordProvider;

        [SetUp]
        public void UserModuleTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _userValidator = Substitute.For<IUserValidator>();
            _passwordProvider = Substitute.For<IPasswordProvider>();

            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<UserViewModel, UserModel>();
            });
            _mapper = config.CreateMapper();
        }

        #region ChangePassword Tests

        [TestCase("")]
        [TestCase("no")]
        public void ChangePassword_InvalidPassword_ReturnsError(string password)
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Length, Is.EqualTo(1)); 
        }

        [Test]
        public void ChangePassword_PasswordDoesNotMatchConfirm_ReturnsError()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Length, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("do not match"), Is.True);
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
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Length, Is.EqualTo(0));

            // make sure the user was updated and saved
            Assert.That(user.Password, Is.EqualTo(newHashedPassword));
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
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                }
                else
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
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
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Length, Is.EqualTo(1));
            _userStore.DidNotReceive().Save();

        }

        [Test]
        public void Save_PasswordsDoNotMatch_ReturnsFailure()
        {
            // setup
            var currentUser = new UserIdentity() { Id = Guid.NewGuid(), UserName = "Joe Soap" };
            currentUser.Claims = new string[] { Claims.UserAdd };
            var browser = new Browser((bootstrapper) =>
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Length, Is.EqualTo(1));
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
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Length, Is.EqualTo(0));
            _userStore.Received(1).Save();

            // the user should have been added
            List<UserModel> users = _userStore.Users;
            Assert.That(users.Count, Is.EqualTo(1));
            Assert.That(users[0].UserName, Is.EqualTo(userName));
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
                bootstrapper.Module(new UserModule(_mapper, _userStore, _userValidator, _passwordProvider))
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
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // check the result
            BasicResult result = JsonConvert.DeserializeObject<BasicResult>(response.Body.AsString());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Length, Is.EqualTo(0));

            _passwordProvider.Received(1).GenerateSalt();
            _passwordProvider.Received(1).HashPassword(password, salt);

            List<UserModel> users = _userStore.Users;
            Assert.That(users[0].Password, Is.EqualTo(hashedPassword));
        }

        #endregion



    }
}
