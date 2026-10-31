using Microsoft.AspNetCore.Authentication.Cookies;
using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using System.Security.Claims;

namespace Stateless.WorkflowEngine.WebConsole.Security
{
    /// <summary>
    /// Builds the cookie principal for a user.  Admin users get all claims, other users only get the claims
    /// assigned to them.
    /// </summary>
    public static class UserPrincipalFactory
    {
        public static ClaimsPrincipal Create(UserModel user)
        {
            var identityClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? String.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? String.Empty)
            };
            identityClaims.AddRange(GetPermissions(user).Select(x => new Claim(Claims.PermissionClaimType, x)));

            var identity = new ClaimsIdentity(identityClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        public static IReadOnlyList<string> GetPermissions(UserModel user)
        {
            if (user.Role == Roles.Admin)
            {
                return Claims.AllClaims;
            }
            return (user.Claims ?? Array.Empty<string>()).Where(x => Claims.AllClaims.Contains(x)).ToList();
        }

        public static Guid? GetUserId(ClaimsPrincipal principal)
        {
            string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out Guid userId) ? userId : null;
        }
    }
}
