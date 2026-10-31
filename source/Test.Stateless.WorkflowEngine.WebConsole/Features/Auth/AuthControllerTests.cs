using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Auth;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;
using System.Security.Claims;

namespace Test.Stateless.WorkflowEngine.WebConsole.Features.Auth
{
    [TestFixture]
    public class AuthControllerTests
    {
        private IUserStore _userStore = null!;
        private IPasswordProvider _passwordProvider = null!;
        private IWebConsoleVersionService _versionService = null!;
        private IAuthenticationService _authenticationService = null!;
        private AuthController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _userStore = Substitute.For<IUserStore>();
            _passwordProvider = Substitute.For<IPasswordProvider>();
            _versionService = Substitute.For<IWebConsoleVersionService>();
            _versionService.GetWebConsoleVersion().Returns("6.0.0");
            _authenticationService = Substitute.For<IAuthenticationService>();

            var services = new ServiceCollection().AddSingleton(_authenticationService).BuildServiceProvider();
            _controller = new AuthController(_userStore, _passwordProvider, _versionService, NullLogger<AuthController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
            };
        }

        // --- Login ---

        [TestCase(null, "pwd")]
        [TestCase("joe", null)]
        [TestCase(" ", "pwd")]
        public async Task Login_Returns400_WhenUserNameOrPasswordMissing(string? userName, string? password)
        {
            var result = await _controller.Login(new LoginRequest { UserName = userName, Password = password });

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Login_Returns400_WhenUserNotFound()
        {
            var result = await _controller.Login(new LoginRequest { UserName = "joe", Password = "pwd" });

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Invalid user name or password" }));
        }

        [Test]
        public async Task Login_Returns400_WhenPasswordIncorrect()
        {
            var user = TestHelpers.CreateUser("joe");
            _userStore.GetUser("joe").Returns(user);
            _passwordProvider.CheckPassword("pwd", user.Password).Returns(false);

            var result = await _controller.Login(new LoginRequest { UserName = "joe", Password = "pwd" });

            Assert.That(TestHelpers.GetErrors(result), Is.EqualTo(new[] { "Invalid user name or password" }));
            await _authenticationService.DidNotReceiveWithAnyArgs().SignInAsync(default!, default, default!, default);
        }

        [Test]
        public async Task Login_SignsInAndReturnsUser_WhenPasswordCorrect()
        {
            var user = TestHelpers.CreateUser("joe", Roles.User, Claims.UserList);
            _userStore.GetUser("joe").Returns(user);
            _passwordProvider.CheckPassword("pwd", user.Password).Returns(true);

            var result = await _controller.Login(new LoginRequest { UserName = "joe", Password = "pwd" });

            var response = (CurrentUserResponse)((OkObjectResult)result).Value!;
            Assert.That(response.Id, Is.EqualTo(user.Id));
            Assert.That(response.Claims, Is.EqualTo(new[] { Claims.UserList }));
            Assert.That(response.Version, Is.EqualTo("6.0.0"));
            await _authenticationService.Received(1).SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(),
                Arg.Is<ClaimsPrincipal>(p => UserPrincipalFactory.GetUserId(p) == user.Id), Arg.Any<AuthenticationProperties>());
        }
    }
}
