using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Mcp.Tools;
using ElixomClaim.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ElixomClaim.Web.Controllers;
[ApiController, Route("mcp/payroll")]
[EnableRateLimiting(RateLimitingConfiguration.McpPolicy)]
[Authorize(AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName, Policy = PolicyNames.RequireAccountant)]
public sealed class McpPayrollController : ControllerBase
{
    private readonly PayrollTools _tools;
    public McpPayrollController(PayrollTools tools) => _tools = tools;
    [HttpPost("preview")] public async Task<IActionResult> Preview(PayrollPreviewRequest request) => Ok(await _tools.PreviewAsync(request, ActorId(), HttpContext.RequestAborted));
    [HttpPost("run")] public async Task<IActionResult> Run(PayrollRunRequest request) => Ok(await _tools.RunAsync(request, ActorId(), HttpContext.RequestAborted));
    private Guid ActorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
