using Newtonsoft.Json;
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
using SystemWrapper.IO;

namespace Test.Stateless.WorkflowEngine.WebConsole.BLL.Data.Stores
{
    [TestFixture]
    public class UserStoreTest
    {
        private IUserStore _userStore;
        private IFileWrap _fileWrap;
        private IPasswordProvider _passwordProvider;
        private const string _path = "dummypath";

        [SetUp]
        public void UserStoreTest_SetUp()
        {
            _fileWrap = Substitute.For<IFileWrap>();
            _passwordProvider = Substitute.For<IPasswordProvider>();

            _userStore = new UserStore(_path, _fileWrap, _passwordProvider);
        }

        #region Load Tests

        [Test]
        public void Load_FileExists_LoadsAndPopulatesStore()
        {
            // setup
            UserModel user = new UserModel();
            user.Id = Guid.NewGuid();
            user.UserName = "test";
            user.Password = "password";
            List<UserModel> users = new List<UserModel>() { user };
            string sUsers = JsonConvert.SerializeObject(users);

            _fileWrap.Exists(_path).Returns(true);
            _fileWrap.ReadAllText(_path).Returns(sUsers);

            // execute
            _userStore.Load();

            // assert
            _fileWrap.Received(1).Exists(_path);
            _fileWrap.Received(1).ReadAllText(_path);
            Assert.AreEqual(1, _userStore.Users.Count);
            Assert.AreEqual(user.Id, _userStore.Users[0].Id);
            Assert.AreEqual(user.UserName, _userStore.Users[0].UserName);
            Assert.IsNotNullOrEmpty(_userStore.Users[0].Password);
        }

        [Test]
        public void Load_FileDoesNotExist_CollectionCreatedWithAdminUser()
        {
            // setup
            _fileWrap.Exists(_path).Returns(false);

            // execute
            _userStore.Load();

            // assert
            _fileWrap.Received(1).Exists(_path);
            _fileWrap.DidNotReceive().ReadAllText(Arg.Any<String>());

            _passwordProvider.Received(1).HashPassword("admin", Arg.Any<string>());
            _passwordProvider.Received(1).GenerateSalt();

            Assert.AreEqual(1, _userStore.Users.Count);
            Assert.AreEqual("admin", _userStore.Users[0].UserName);

        }

        #endregion
    }
}
