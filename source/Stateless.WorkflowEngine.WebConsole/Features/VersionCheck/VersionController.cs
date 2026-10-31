using Microsoft.AspNetCore.Mvc;
using Stateless.WorkflowEngine.WebConsole.Services;

namespace Stateless.WorkflowEngine.WebConsole.Features.VersionCheck
{
    [Route("api/version")]
    public class VersionController : AuthenticatedBaseController
    {
        private readonly IVersionCheckService _versionCheckService;
        private readonly IWebConsoleVersionService _webConsoleVersionService;
        private readonly ILogger<VersionController> _logger;

        public VersionController(IVersionCheckService versionCheckService, IWebConsoleVersionService webConsoleVersionService, ILogger<VersionController> logger)
        {
            _versionCheckService = versionCheckService;
            _webConsoleVersionService = webConsoleVersionService;
            _logger = logger;
        }

        [HttpGet("check")]
        public async Task<IActionResult> Check()
        {
            try
            {
                return Ok(await _versionCheckService.CheckIfNewVersionAvailable());
            }
            catch (Exception ex)
            {
                // the console may not have internet access - this is not an error worth surfacing to the user
                _logger.LogWarning(ex, "Unable to check for a new version");
                return Ok(new VersionCheckResult { InstalledVersionNumber = _webConsoleVersionService.GetWebConsoleVersion() });
            }
        }
    }
}
