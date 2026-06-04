using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.ViewModels.User;
using System;

namespace Test.Stateless.WorkflowEngine.WebConsole.ViewModels.User
{
    [TestFixture]
    public class UserMappingExtensionsTest
    {
        #region ToUserModel Tests

        [Test]
        public void ToUserModel_MapsAllProperties()
        {
            UserViewModel viewModel = new UserViewModel
            {
                Id = Guid.NewGuid(),
                UserName = "testuser",
                Password = "password123",
                Role = "admin",
            };

            UserModel result = viewModel.ToUserModel();

            Assert.That(result.Id, Is.EqualTo(viewModel.Id));
            Assert.That(result.UserName, Is.EqualTo(viewModel.UserName));
            Assert.That(result.Password, Is.EqualTo(viewModel.Password));
            Assert.That(result.Role, Is.EqualTo(viewModel.Role));
        }

        #endregion
    }
}
