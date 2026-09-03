using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[ApiController, Route("mcp/collections")]
[Authorize(AuthenticationSchemes = BearerTokenAuthenticationHandler.SchemeName)]
public sealed class McpCollectionsController : ControllerBase
{
    private readonly CollectionTools _tools;
    private readonly ApplicationDbContext _db;

    public McpCollectionsController(CollectionTools tools, ApplicationDbContext db)
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
    public async Task<IActionResult> List(ListCollectionsRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.ListCollectionsAsync(actor, request, HttpContext.RequestAborted));
    }

    [HttpPost("get")]
    public async Task<IActionResult> Get(GetCollectionRequest request)
    {
        if (!HasMcpScope()) return Forbid();
        var actor = await GetActorUserAsync();
        if (actor == null) return Unauthorized();
        return Ok(await _tools.GetCollectionAsync(actor, request, HttpContext.RequestAborted));
    }
}
