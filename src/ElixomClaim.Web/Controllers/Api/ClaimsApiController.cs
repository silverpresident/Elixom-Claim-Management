using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController]
[Route("api/v1/claims")]
[Authorize(Policy = "ApiAccess")]
public sealed class ClaimsApiController : ControllerBase
{
    private readonly IClaimService _claims;
    private readonly IActorResolver _actors;
    private readonly ILogger<ClaimsApiController> _logger;

    public ClaimsApiController(IClaimService claims, IActorResolver actors, ILogger<ClaimsApiController> logger)
        => (_claims, _actors, _logger) = (claims, actors, logger);

    [HttpGet]
    public async Task<ActionResult<ApiClaimPage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] ClaimStatus? status = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "page must be positive and pageSize must be between 1 and 100.");
        var actor = await ResolveAsync(ct); if (actor is null) return Forbid();
        var claims = actor.Role.HasMinimumRole(UserRole.Manager)
            ? await _claims.GetQueueClaimsAsync(status, ct)
            : await _claims.GetUserClaimsAsync(actor.Id, ct);
        if (status.HasValue && !actor.Role.HasMinimumRole(UserRole.Manager)) claims = claims.Where(c => c.Status == status).ToList();
        var total = claims.Count;
        return Ok(new ApiClaimPage(page, pageSize, total, claims.OrderByDescending(c => c.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(ApiClaim.From).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiClaim>> Get(Guid id, CancellationToken ct)
    {
        var actor = await ResolveAsync(ct); if (actor is null) return Forbid();
        var claim = await _claims.GetByIdAsync(id, actor, ct);
        return claim is null ? NotFound() : Ok(ApiClaim.From(claim));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var actor = await ResolveAsync(ct); if (actor is null) return Forbid();
        if (!await _claims.SubmitAsync(new SubmitClaimCommand(id, actor.Id), ct)) return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Only an owned draft claim can be submitted.");
        await _actors.LogAuditAsync(new ActorContext(actor, "api", "api:access", HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", false), "API_CLAIM_SUBMIT", $"Claim:{id}", cancellationToken: ct);
        _logger.LogInformation("API claim {ClaimId} submitted by {ActorId}.", id, actor.Id);
        return NoContent();
    }

    private async Task<User?> ResolveAsync(CancellationToken ct)
    {
        var actor = await _actors.ResolveActorAsync(HttpContext, "api:access", false, ct);
        return actor.IsSuccess ? actor.Value!.User : null;
    }
}

public sealed record ApiClaim(Guid Id, long SequenceNo, string Title, string Description, decimal Amount, ClaimStatus Status, ClaimPaymentStatus PaymentStatus, DateTime CreatedAtUtc)
{
    public static ApiClaim From(Claim claim) => new(claim.Id, claim.SequenceNo, claim.Title, claim.Description, claim.Amount, claim.Status, claim.PaymentStatus, claim.CreatedAtUtc);
}

public sealed record ApiClaimPage(int Page, int PageSize, int TotalCount, IReadOnlyList<ApiClaim> Items);
