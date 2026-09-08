using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController, Route("api/v1/operations"), Authorize(Policy = "ApiAccess")]
public sealed class OperationsApiController(IOperationRecordService operations, IActorResolver actors, ILogger<OperationsApiController> logger) : ControllerBase
{
    [HttpGet("{idempotencyKey}")]
    public async Task<ActionResult<ApiOperation>> Get(string idempotencyKey, CancellationToken ct)
    {
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var record = await operations.GetForActorAsync(idempotencyKey, actor.Value!.User.Id.ToString(), ct); if (record is null) return NotFound();
        logger.LogInformation("API operation {OperationType} read by {ActorId}", record.OperationType, actor.Value.User.Id);
        return Ok(ApiOperation.From(record));
    }
}
public sealed record ApiOperation(string IdempotencyKey, string OperationType, string Status, string? Details, DateTime ExecutedAtUtc) { public static ApiOperation From(OperationRecord value) => new(value.IdempotencyKey, value.OperationType, value.Status, value.Details, value.ExecutedAtUtc); }
