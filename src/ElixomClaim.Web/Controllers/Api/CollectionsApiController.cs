using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController, Route("api/v1/collections"), Authorize(Policy = "ApiAccess")]
public sealed class CollectionsApiController(ICollectionService collections, IActorResolver actors, ILogger<CollectionsApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiCollectionPage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? clientId = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return Problem(statusCode: 400, detail: "page must be positive and pageSize must be between 1 and 100.");
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = await collections.ListForActorAsync(actor.Value!.User.Id, clientId, 100, ct);
        if (result.IsFailure) return Forbid();
        var all = result.Value!; return Ok(new ApiCollectionPage(page, pageSize, all.Count, all.Skip((page - 1) * pageSize).Take(pageSize).Select(ApiCollection.From).ToList()));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiCollection>> Get(Guid id, CancellationToken ct)
    {
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = await collections.GetForActorAsync(actor.Value!.User.Id, id, ct); if (result.IsFailure) return NotFound();
        logger.LogInformation("API collection {CollectionId} read by {ActorId}", id, actor.Value.User.Id); return Ok(ApiCollection.From(result.Value!));
    }
}
public sealed record ApiCollection(Guid Id, long SequenceNo, Guid CollectionClientId, string PayorName, string Method, string Status, decimal Amount, string Currency, DateTime PaymentDateUtc, DateTime CreatedAtUtc) { public static ApiCollection From(CollectionReadModel value) => new(value.Id, value.SequenceNo, value.CollectionClientId, value.PayorName, value.Method.ToString(), value.Status.ToString(), value.Amount, value.Currency, value.PaymentDateUtc, value.CreatedAtUtc); }
public sealed record ApiCollectionPage(int Page, int PageSize, int TotalCount, IReadOnlyList<ApiCollection> Items);
