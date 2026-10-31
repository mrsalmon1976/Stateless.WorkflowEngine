using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Stateless.WorkflowEngine.WebConsole.Features.Auth
{
    public class LoginRequest
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }
    }

    public class CurrentUserResponse
    {
        public Guid Id { get; set; }

        public string? UserName { get; set; }

        public string? Role { get; set; }

        public IReadOnlyList<string> Claims { get; set; } = Array.Empty<string>();

        public string? Version { get; set; }
    }

    [Route("api/auth")]
    public class AuthController : AuthenticatedBaseController
    {
        private readonly IUserStore _userStore;
        private readonly IPasswordProvider _passwordProvider;
        private readonly IWebConsoleVersionService _versionService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserStore userStore, IPasswordProvider passwordProvider, IWebConsoleVersionService versionService, ILogger<AuthController> logger)
        {
            _userStore = userStore;
            _passwordProvider = passwordProvider;
            _versionService = versionService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (String.IsNullOrWhiteSpace(request.UserName) || String.IsNullOrEmpty(request.Password))
            {
                return ValidationFailed("User name and password are required");
            }

            UserModel? user = _userStore.GetUser(request.UserName);
            if (user == null || !_passwordProvider.CheckPassword(request.Password, user.Password))
            {
                _logger.LogWarning("Failed login attempt for user {UserName}", request.UserName);
                return ValidationFailed("Invalid user name or password");
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserPrincipalFactory.Create(user));
            return Ok(CreateResponse(user));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            Guid? userId = GetCurrentUserId();
            UserModel? user = userId.HasValue ? _userStore.GetUser(userId.Value) : null;
            if (user == null)
            {
                return Unauthorized();
            }
            return Ok(CreateResponse(user));
        }

        private CurrentUserResponse CreateResponse(UserModel user)
        {
            return new CurrentUserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Role = user.Role,
                Claims = UserPrincipalFactory.GetPermissions(user),
                Version = _versionService.GetWebConsoleVersion()
            };
        }
    }
}
