using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[ApiController, Route("mcp/operations")]
[Authorize(AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName)]
public sealed class McpOperationsController : ControllerBase
{
    private readonly OperationsTools _tools;
    private readonly ApplicationDbContext _db;

    public McpOperationsController(OperationsTools tools, ApplicationDbContext db)
    {
        _tools = tools;
        _db = db;
    }

    private async Task<User?> GetActorUserAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId)) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    private bool HasMcpScope()
    {
        var scopeClaim = User.FindFirstValue("scope");
        return !string.IsNullOrEmpty(scopeClaim) && scopeClaim.Split(' ').Contains("mcp:access");
    }

    [HttpPost("salary-gen")]
    public async Task<IActionResult> RequestSalaryGen(SalaryGenCommandRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.RequestSalaryGenerationAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("outbox-wakeup")]
    public async Task<IActionResult> RequestOutboxWakeUp(OutboxWakeUpRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.RequestOutboxWakeUpAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("status")]
    public async Task<IActionResult> GetStatus(OperationStatusRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.GetOperationStatusAsync(actor, request, HttpContext.RequestAborted));
    }
}
