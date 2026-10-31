using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Stateless.WorkflowEngine.WebConsole.Features.Users
{
    public class UserRequest
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string? ConfirmPassword { get; set; }

        public string? Role { get; set; }

        public string[]? Claims { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string? Password { get; set; }

        public string? ConfirmPassword { get; set; }
    }

    public class UserResponse
    {
        public Guid Id { get; set; }

        public string? UserName { get; set; }

        public string? Role { get; set; }

        public IReadOnlyList<string> Claims { get; set; } = Array.Empty<string>();

        public static UserResponse FromModel(UserModel model)
        {
            return new UserResponse
            {
                Id = model.Id,
                UserName = model.UserName,
                Role = model.Role,
                Claims = model.Claims ?? Array.Empty<string>()
            };
        }
    }

    public class UserValidator
    {
        public const int MinPasswordLength = 6;

        private readonly IUserStore _userStore;

        public UserValidator(IUserStore userStore)
        {
            _userStore = userStore;
        }

        /// <summary>
        /// Validates a user request.  When updating an existing user, a blank password means the password
        /// is not being changed.
        /// </summary>
        public virtual IReadOnlyList<string> Validate(UserRequest request, Guid? existingUserId)
        {
            var errors = new List<string>();

            if (String.IsNullOrWhiteSpace(request.UserName))
            {
                errors.Add("User name cannot be empty");
            }
            else
            {
                UserModel? existingUser = _userStore.GetUser(request.UserName.Trim());
                if (existingUser != null && existingUser.Id != existingUserId)
                {
                    errors.Add("A user with the supplied user name already exists");
                }
            }

            if (!existingUserId.HasValue || !String.IsNullOrEmpty(request.Password))
            {
                errors.AddRange(ValidatePassword(request.Password, request.ConfirmPassword));
            }

            if (String.IsNullOrWhiteSpace(request.Role))
            {
                errors.Add("Role cannot be empty");
            }
            else if (!Roles.AllRoles.Contains(request.Role))
            {
                errors.Add($"Role '{request.Role}' is not valid");
            }

            var invalidClaims = (request.Claims ?? Array.Empty<string>()).Where(x => !Security.Claims.AllClaims.Contains(x)).ToList();
            if (invalidClaims.Count > 0)
            {
                errors.Add($"Invalid claims: {String.Join(", ", invalidClaims)}");
            }

            return errors;
        }

        public virtual IReadOnlyList<string> ValidatePassword(string? password, string? confirmPassword)
        {
            var errors = new List<string>();
            if (String.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                errors.Add($"Password must be at least {MinPasswordLength} characters");
            }
            else if (password != confirmPassword)
            {
                errors.Add("Password and confirmation password do not match");
            }
            return errors;
        }
    }
}
