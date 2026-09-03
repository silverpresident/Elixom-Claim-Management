using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[ApiController, Route("mcp/email")]
[Authorize(AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName)]
public sealed class McpEmailController : ControllerBase
{
    private readonly EmailTools _tools;
    private readonly ApplicationDbContext _db;

    public McpEmailController(EmailTools tools, ApplicationDbContext db)
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

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(EmailPreviewRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.PreviewAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("queue-send")]
    public async Task<IActionResult> QueueSend(EmailQueueSendRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.QueueSendAsync(actor, request, HttpContext.RequestAborted));
    }
}
