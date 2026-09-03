using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[ApiController, Route("mcp/claims")]
[Authorize(AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName)]
public sealed class McpClaimsController : ControllerBase
{
    private readonly ClaimTools _tools;
    private readonly ApplicationDbContext _db;

    public McpClaimsController(ClaimTools tools, ApplicationDbContext db)
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

    [HttpPost("list")]
    public async Task<IActionResult> List(ListClaimsRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.ListClaimsAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("get")]
    public async Task<IActionResult> Get(GetClaimRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.GetClaimAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(SubmitClaimRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.SubmitClaimAsync(actor, request, HttpContext.RequestAborted));
    }
}
