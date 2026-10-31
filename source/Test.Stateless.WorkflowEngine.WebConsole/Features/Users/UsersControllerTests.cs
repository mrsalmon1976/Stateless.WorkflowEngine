using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Users;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Test.Stateless.WorkflowEngine.WebConsole.Features.Users
{
    [TestFixture]
    public class UsersControllerTests
    {
        private IUserStore _userStore = null!;
        private UserValidator _validator = null!;
        private IPasswordProvider _passwordProvider = null!;
        private UserModel _currentUser = null!;
        private UsersController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _validator = Substitute.For<UserValidator>(_userStore);
            _validator.Validate(Arg.Any<UserRequest>(), Arg.Any<Guid?>()).Returns(new List<string>());
            _validator.ValidatePassword(Arg.Any<string?>(), Arg.Any<string?>()).Returns(new List<string>());
            _passwordProvider = Substitute.For<IPasswordProvider>();
            _passwordProvider.HashPassword(Arg.Any<string>()).Returns(x => "hashed:" + x.Arg<string>());
            _currentUser = TestHelpers.CreateUser("admin", Roles.Admin);
            _userStore.GetUser(_currentUser.Id).Returns(_currentUser);
            _controller = new UsersController(_userStore, _validator, _passwordProvider).WithUser(_currentUser);
        }

        // --- List ---

        [Test]
        public void List_ReturnsUsersWithoutPasswords()
        {
            _userStore.Users.Returns(new[] { TestHelpers.CreateUser("b"), TestHelpers.CreateUser("a") });

            var result = (OkObjectResult)_controller.List();

            var users = ((IEnumerable<UserResponse>)result.Value!).ToList();
            Assert.That(users.Select(x => x.UserName), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(typeof(UserResponse).GetProperty("Password"), Is.Null);
        }

        // --- Add ---

        [Test]
        public void Add_Returns400_WhenInvalid()
        {
            _validator.Validate(Arg.Any<UserRequest>(), null).Returns(new List<string> { "User name cannot be empty" });

            var result = _controller.Add(new UserRequest());

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "User name cannot be empty" }));
            _userStore.DidNotReceiveWithAnyArgs().SaveUser(default!);
        }

        [Test]
        public void Add_HashesPasswordAndSavesClaims()
        {
            var result = _controller.Add(new UserRequest { UserName = " joe ", Password = "secret", ConfirmPassword = "secret", Role = Roles.User, Claims = new[] { Claims.UserList } });

            Assert.That(result, Is.TypeOf<OkObjectResult>());
            _userStore.Received(1).SaveUser(Arg.Is<UserModel>(x => x.UserName == "joe" && x.Password == "hashed:secret" && x.Role == Roles.User && x.Claims!.Single() == Claims.UserList));
        }

        [Test]
        public void Add_ClearsClaims_ForAdmins()
        {
            _controller.Add(new UserRequest { UserName = "joe", Password = "secret", Role = Roles.Admin, Claims = new[] { Claims.UserList } });

            _userStore.Received(1).SaveUser(Arg.Is<UserModel>(x => x.Claims!.Length == 0));
        }

        // --- Update ---

        [Test]
        public void Update_Returns404_WhenUserNotFound()
        {
            Assert.That(_controller.Update(Guid.NewGuid(), new UserRequest()), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Update_KeepsPassword_WhenNotSupplied()
        {
            var existing = TestHelpers.CreateUser("joe");
            _userStore.GetUser(existing.Id).Returns(existing);

            _controller.Update(existing.Id, new UserRequest { UserName = "joe", Role = Roles.User });

            _userStore.Received(1).SaveUser(Arg.Is<UserModel>(x => x.Id == existing.Id && x.Password == existing.Password));
            _passwordProvider.DidNotReceiveWithAnyArgs().HashPassword(default!);
        }

        [Test]
        public void Update_ChangesPassword_WhenSupplied()
        {
            var existing = TestHelpers.CreateUser("joe");
            _userStore.GetUser(existing.Id).Returns(existing);

            _controller.Update(existing.Id, new UserRequest { UserName = "joe", Password = "newpwd", ConfirmPassword = "newpwd", Role = Roles.User });

            _userStore.Received(1).SaveUser(Arg.Is<UserModel>(x => x.Password == "hashed:newpwd"));
        }

        [Test]
        public void Update_Returns400_WhenDemotingLastAdmin()
        {
            _userStore.Users.Returns(new[] { _currentUser, TestHelpers.CreateUser("joe") });

            var result = _controller.Update(_currentUser.Id, new UserRequest { UserName = "admin", Role = Roles.User });

            Assert.That(TestHelpers.GetErrors(result).Single(), Does.Contain("last administrator"));
            _userStore.DidNotReceiveWithAnyArgs().SaveUser(default!);
        }

        [Test]
        public void Update_AllowsDemotingAdmin_WhenAnotherAdminExists()
        {
            var otherAdmin = TestHelpers.CreateUser("other", Roles.Admin);
            _userStore.Users.Returns(new[] { _currentUser, otherAdmin });

            var result = _controller.Update(_currentUser.Id, new UserRequest { UserName = "admin", Role = Roles.User });

            Assert.That(result, Is.TypeOf<OkObjectResult>());
        }

        // --- Delete ---

        [Test]
        public void Delete_Returns404_WhenUserNotFound()
        {
            Assert.That(_controller.Delete(Guid.NewGuid()), Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public void Delete_Returns400_WhenDeletingSelf()
        {
            _userStore.Users.Returns(new[] { _currentUser, TestHelpers.CreateUser("other", Roles.Admin) });

            var result = _controller.Delete(_currentUser.Id);

            Assert.That(TestHelpers.GetErrors(result).Single(), Does.Contain("your own account"));
            _userStore.DidNotReceiveWithAnyArgs().DeleteUser(default);
        }

        [Test]
        public void Delete_Returns400_WhenDeletingLastAdmin()
        {
            var limitedController = new UsersController(_userStore, _validator, _passwordProvider).WithUser(TestHelpers.CreateUser("deleter", Roles.User, Claims.UserDelete));
            _userStore.Users.Returns(new[] { _currentUser });

            var result = limitedController.Delete(_currentUser.Id);

            Assert.That(TestHelpers.GetErrors(result).Single(), Does.Contain("last administrator"));
        }

        [Test]
        public void Delete_RemovesUser()
        {
            var user = TestHelpers.CreateUser("joe");
            _userStore.GetUser(user.Id).Returns(user);

            var result = _controller.Delete(user.Id);

            Assert.That(result, Is.TypeOf<NoContentResult>());
            _userStore.Received(1).DeleteUser(user.Id);
        }

        // --- ChangePassword ---

        [Test]
        public void ChangePassword_Returns400_WhenInvalid()
        {
            _validator.ValidatePassword("abc", "abc").Returns(new List<string> { "too short" });

            var result = _controller.ChangePassword(new ChangePasswordRequest { Password = "abc", ConfirmPassword = "abc" });

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "too short" }));
        }

        [Test]
        public void ChangePassword_SavesHashedPasswordForCurrentUser()
        {
            var result = _controller.ChangePassword(new ChangePasswordRequest { Password = "newpwd", ConfirmPassword = "newpwd" });

            Assert.That(result, Is.TypeOf<NoContentResult>());
            _userStore.Received(1).SaveUser(Arg.Is<UserModel>(x => x.Id == _currentUser.Id && x.Password == "hashed:newpwd" && x.Role == Roles.Admin));
        }
    }

    [TestFixture]
    public class UserValidatorTests
    {
        private IUserStore _userStore = null!;
        private UserValidator _validator = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _validator = new UserValidator(_userStore);
        }

        [Test]
        public void Validate_ReturnsNoErrors_WhenValid()
        {
            Assert.That(_validator.Validate(CreateValidRequest(), null), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Validate_ReturnsError_WhenUserNameEmpty(string? userName)
        {
            var request = CreateValidRequest();
            request.UserName = userName;

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { "User name cannot be empty" }));
        }

        [Test]
        public void Validate_ReturnsError_WhenUserNameExists()
        {
            _userStore.GetUser("joe").Returns(TestHelpers.CreateUser("joe"));

            Assert.That(_validator.Validate(CreateValidRequest(), null), Is.EqualTo(new[] { "A user with the supplied user name already exists" }));
        }

        [Test]
        public void Validate_AllowsSameUserName_WhenUpdatingSameUser()
        {
            var existing = TestHelpers.CreateUser("joe");
            _userStore.GetUser("joe").Returns(existing);

            Assert.That(_validator.Validate(CreateValidRequest(), existing.Id), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("12345")]
        public void Validate_ReturnsError_WhenNewUserPasswordTooShort(string? password)
        {
            var request = CreateValidRequest();
            request.Password = password;
            request.ConfirmPassword = password;

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { "Password must be at least 6 characters" }));
        }

        [Test]
        public void Validate_AllowsBlankPassword_WhenUpdating()
        {
            var request = CreateValidRequest();
            request.Password = null;
            request.ConfirmPassword = null;

            Assert.That(_validator.Validate(request, Guid.NewGuid()), Is.Empty);
        }

        [Test]
        public void Validate_ReturnsError_WhenPasswordsDoNotMatch()
        {
            var request = CreateValidRequest();
            request.ConfirmPassword = "different";

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { "Password and confirmation password do not match" }));
        }

        [TestCase(null, "Role cannot be empty")]
        [TestCase("SuperUser", "Role 'SuperUser' is not valid")]
        public void Validate_ReturnsError_WhenRoleInvalid(string? role, string expected)
        {
            var request = CreateValidRequest();
            request.Role = role;

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void Validate_ReturnsError_WhenClaimsInvalid()
        {
            var request = CreateValidRequest();
            request.Claims = new[] { Claims.UserList, "Bogus" };

            Assert.That(_validator.Validate(request, null), Is.EqualTo(new[] { "Invalid claims: Bogus" }));
        }

        private static UserRequest CreateValidRequest()
        {
            return new UserRequest { UserName = "joe", Password = "secret", ConfirmPassword = "secret", Role = Roles.User };
        }
    }
}
