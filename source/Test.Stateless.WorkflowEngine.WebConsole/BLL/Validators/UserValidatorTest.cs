using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Validators
{
    [TestFixture]
    public class UserValidatorTest
    {
        private IUserValidator _userValidator;

        private IUserStore _userStore;

        [SetUp]
        public void UserValidatorTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();

            _userValidator = new UserValidator(_userStore);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void Validate_InvalidUserName_ReturnsFailure(string userName)
        {
            UserModel model = DataHelper.CreateUserModel();
            model.UserName = userName;

            ValidationResult result = _userValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("User name"), Is.True);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void Validate_InvalidUserPassword_ReturnsFailure(string password)
        {
            UserModel model = DataHelper.CreateUserModel();
            model.Password = password;

            ValidationResult result = _userValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Password"), Is.True);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void Validate_InvalidRole_ReturnsFailure(string role)
        {
            UserModel model = DataHelper.CreateUserModel();
            model.Role = role;

            ValidationResult result = _userValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("Role"), Is.True);
        }

        [Test]
        public void Validate_UserAlreadyExists_ReturnsFailure()
        {
            UserModel model = DataHelper.CreateUserModel();

            _userStore.GetUser(model.UserName).Returns(new UserModel());

            ValidationResult result = _userValidator.Validate(model);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Contains("already exists"), Is.True);
        }

        [Test]
        public void Validate_AllFieldsValid_ReturnsSuccess()
        {
            UserModel model = DataHelper.CreateUserModel();

            ValidationResult result = _userValidator.Validate(model);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Messages.Count, Is.EqualTo(0));
        }


    }
}
