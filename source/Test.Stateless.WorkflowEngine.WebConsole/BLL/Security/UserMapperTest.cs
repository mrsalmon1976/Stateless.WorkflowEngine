using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Security;
using NSubstitute;
using NUnit.Framework;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores;
using Stateless.WorkflowEngine.WebConsole.BLL.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Security
{
    [TestFixture]
    public class UserMapperTest
    {
        private IUserStore _userStore;
        private IUserMapper _userMapper;

        [SetUp]
        public void UserMapperTest_SetUp()
        {
            _userStore = Substitute.For<IUserStore>();

            _userMapper = new UserMapper(_userStore);
        }

        [Test]
        public void GetUserFromIdentifier_UserNotFound_ReturnsNull()
        {
            _userStore.Users.Returns(new List<UserModel>());
            IUserIdentity userIdentity = _userMapper.GetUserFromIdentifier(Guid.NewGuid(), new NancyContext());
            Assert.That(userIdentity, Is.Null);
        }

        [Test]
        public void GetUserFromIdentifier_UserInAdminRole_ReturnsUserWithAllClaims()
        {
            // setup the user with no claims
            UserModel user = new UserModel();
            user.UserName = Guid.NewGuid().ToString();
            user.Password = Guid.NewGuid().ToString();
            user.Role = Roles.Admin;
            user.Claims = Enumerable.Empty<string>().ToArray();
            _userStore.Users.Returns(new List<UserModel>() { user });

            // execute 
            IUserIdentity userIdentity = _userMapper.GetUserFromIdentifier(user.Id, new NancyContext());

            // assert
            Assert.That(userIdentity, Is.Not.Null);

            Assert.That(userIdentity.UserName, Is.EqualTo(user.UserName));
            foreach (string claim in Claims.AllClaims)
            {
                Assert.That(userIdentity.Claims.ToList().Contains(claim), Is.True);
            }
        }

        [Test]
        public void GetUserFromIdentifier_UserInUserRole_ReturnsUserWithRestrictedClaims()
        {
            // setup the user with only one claim
            UserModel user = new UserModel();
            user.UserName = Guid.NewGuid().ToString();
            user.Password = Guid.NewGuid().ToString();
            user.Role = Roles.Admin;
            user.Claims = new string[] { Claims.ConnectionAdd };
            _userStore.Users.Returns(new List<UserModel>() { user });

            // execute 
            IUserIdentity userIdentity = _userMapper.GetUserFromIdentifier(user.Id, new NancyContext());

            // assert
            Assert.That(userIdentity, Is.Not.Null);

            // make sure the user has been mapped and has the supplied claim
            Assert.That(userIdentity.UserName, Is.EqualTo(user.UserName));
            Assert.That(userIdentity.Claims.ToList().Contains(Claims.ConnectionAdd), Is.True);

            // user should not have any other claims
            foreach (string claim in Claims.AllClaims.Where(x => x != Claims.ConnectionAdd))
            {
                Assert.That(userIdentity.Claims.ToList().Contains(claim), Is.True);
            }
        }

    }
}
