using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Stateless.WorkflowEngine.WebConsole.Features.Users
{
    [Route("api/users")]
    public class UsersController : AuthenticatedBaseController
    {
        private readonly IUserStore _userStore;
        private readonly UserValidator _validator;
        private readonly IPasswordProvider _passwordProvider;

        public UsersController(IUserStore userStore, UserValidator validator, IPasswordProvider passwordProvider)
        {
            _userStore = userStore;
            _validator = validator;
            _passwordProvider = passwordProvider;
        }

        [HttpGet]
        [Authorize(Policy = Claims.UserList)]
        public IActionResult List()
        {
            return Ok(_userStore.Users.OrderBy(x => x.UserName).Select(UserResponse.FromModel));
        }

        [HttpPost]
        [Authorize(Policy = Claims.UserAdd)]
        public IActionResult Add([FromBody] UserRequest request)
        {
            var errors = _validator.Validate(request, null);
            if (errors.Count > 0) return ValidationFailed(errors);

            var user = new UserModel
            {
                UserName = request.UserName!.Trim(),
                Password = _passwordProvider.HashPassword(request.Password!),
                Role = request.Role,
                Claims = GetClaims(request)
            };
            _userStore.SaveUser(user);
            return Ok(UserResponse.FromModel(user));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = Claims.UserEdit)]
        public IActionResult Update(Guid id, [FromBody] UserRequest request)
        {
            UserModel? existing = _userStore.GetUser(id);
            if (existing == null) return UserNotFound();

            var errors = _validator.Validate(request, id);
            if (errors.Count > 0) return ValidationFailed(errors);

            if (existing.Role == Roles.Admin && request.Role != Roles.Admin && IsLastAdmin(id))
            {
                return ValidationFailed("The last administrator cannot be changed to a non-administrator role");
            }

            var user = new UserModel
            {
                Id = existing.Id,
                UserName = request.UserName!.Trim(),
                Password = String.IsNullOrEmpty(request.Password) ? existing.Password : _passwordProvider.HashPassword(request.Password),
                Role = request.Role,
                Claims = GetClaims(request)
            };
            _userStore.SaveUser(user);
            return Ok(UserResponse.FromModel(user));
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = Claims.UserDelete)]
        public IActionResult Delete(Guid id)
        {
            UserModel? existing = _userStore.GetUser(id);
            if (existing == null) return UserNotFound();

            if (id == GetCurrentUserId())
            {
                return ValidationFailed("You cannot delete your own account");
            }
            if (existing.Role == Roles.Admin && IsLastAdmin(id))
            {
                return ValidationFailed("The last administrator cannot be deleted");
            }

            _userStore.DeleteUser(id);
            return NoContent();
        }

        [HttpPost("me/password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
        {
            Guid? userId = GetCurrentUserId();
            UserModel? existing = userId.HasValue ? _userStore.GetUser(userId.Value) : null;
            if (existing == null) return Unauthorized();

            var errors = _validator.ValidatePassword(request.Password, request.ConfirmPassword);
            if (errors.Count > 0) return ValidationFailed(errors);

            var user = new UserModel
            {
                Id = existing.Id,
                UserName = existing.UserName,
                Password = _passwordProvider.HashPassword(request.Password!),
                Role = existing.Role,
                Claims = existing.Claims
            };
            _userStore.SaveUser(user);
            return NoContent();
        }

        private bool IsLastAdmin(Guid userId)
        {
            return !_userStore.Users.Any(x => x.Role == Roles.Admin && x.Id != userId);
        }

        private static string[] GetClaims(UserRequest request)
        {
            // admins implicitly have all claims
            return request.Role == Roles.Admin ? Array.Empty<string>() : (request.Claims ?? Array.Empty<string>()).Distinct().ToArray();
        }

        private IActionResult UserNotFound()
        {
            return NotFound(new { error = "User not found" });
        }
    }
}
