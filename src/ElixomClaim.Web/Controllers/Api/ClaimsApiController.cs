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
    private readonly IOperationRecordService _operations;
    private readonly ILogger<ClaimsApiController> _logger;

    public ClaimsApiController(IClaimService claims, IActorResolver actors, IOperationRecordService operations, ILogger<ClaimsApiController> logger)
        => (_claims, _actors, _operations, _logger) = (claims, actors, operations, logger);

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
        var reservation = await ReserveAsync("claim-submit", actor.Id, ct); if (reservation is null) return Problem(statusCode: 400, detail: "An Idempotency-Key header is required.");
        if (!reservation.IsNew) return Accepted(new { operation = reservation.Record.IdempotencyKey, status = reservation.Record.Status });
        if (!await _claims.SubmitAsync(new SubmitClaimCommand(id, actor.Id), ct))
        {
            await _operations.UpdateStatusAsync(reservation.Record.Id, "Failed", "Only an owned draft claim can be submitted.", ct);
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Only an owned draft claim can be submitted.");
        }
        await _operations.UpdateStatusAsync(reservation.Record.Id, "Completed", $"Claim:{id}", ct);
        await _actors.LogAuditAsync(new ActorContext(actor, "api", "api:access", HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", false), "API_CLAIM_SUBMIT", new AuditEntity("Claim", id.ToString()), cancellationToken: ct);
        _logger.LogInformation("API claim {ClaimId} submitted by {ActorId}.", id, actor.Id);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<ApiClaim>> Create([FromBody] CreateApiClaimRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description) || request.Amount <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "title, description, and a positive amount are required.");
        var actor = await ResolveAsync(ct); if (actor is null) return Forbid();
        var reservation = await ReserveAsync("claim-create", actor.Id, ct); if (reservation is null) return Problem(statusCode: 400, detail: "An Idempotency-Key header is required.");
        if (!reservation.IsNew) return Accepted(new { operation = reservation.Record.IdempotencyKey, status = reservation.Record.Status });
        var claim = await _claims.CreateDraftAsync(new CreateClaimCommand(actor.Id, request.Title, request.Description, request.Amount, request.DateOfJob), ct);
        await _operations.UpdateStatusAsync(reservation.Record.Id, "Completed", $"Claim:{claim.Id}", ct);
        _logger.LogInformation("API claim {ClaimId} created by {ActorId}.", claim.Id, actor.Id);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, ApiClaim.From(claim));
    }

    private async Task<User?> ResolveAsync(CancellationToken ct)
    {
        var actor = await _actors.ResolveActorAsync(HttpContext, "api:access", false, ct);
        return actor.IsSuccess ? actor.Value!.User : null;
    }

    private async Task<OperationReservation?> ReserveAsync(string type, Guid actorId, CancellationToken ct)
    {
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(key) ? null : await _operations.ReserveAsync($"api:{type}:{actorId:N}:{key.Trim()}", type, actorId.ToString(), ct);
    }
}

public sealed record ApiClaim(Guid Id, long SequenceNo, string Title, string Description, decimal Amount, ClaimStatus Status, ClaimPaymentStatus PaymentStatus, DateTime CreatedAtUtc)
{
    public static ApiClaim From(Claim claim) => new(claim.Id, claim.SequenceNo, claim.Title, claim.Description, claim.Amount, claim.Status, claim.PaymentStatus, claim.CreatedAtUtc);
}

public sealed record ApiClaimPage(int Page, int PageSize, int TotalCount, IReadOnlyList<ApiClaim> Items);
public sealed record CreateApiClaimRequest(string Title, string Description, decimal Amount, DateTime? DateOfJob);
