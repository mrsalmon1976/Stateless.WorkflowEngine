using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Security;

namespace Stateless.WorkflowEngine.WebConsole.Features
{
    [Authorize]
    [ApiController]
    public abstract class AuthenticatedBaseController : ControllerBase
    {
        protected Guid? GetCurrentUserId()
        {
            return UserPrincipalFactory.GetUserId(User);
        }

        protected IActionResult ValidationFailed(IEnumerable<string> errors)
        {
            return BadRequest(new { errors });
        }

        protected IActionResult ValidationFailed(string error)
        {
            return BadRequest(new { errors = new[] { error } });
        }
    }
}
