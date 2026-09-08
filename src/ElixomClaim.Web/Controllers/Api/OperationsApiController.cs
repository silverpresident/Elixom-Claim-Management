using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController, Route("api/v1/operations"), Authorize(Policy = "ApiAccess")]
public sealed class OperationsApiController(IOperationRecordService operations, IApprovedOperationService approvedOperations, IActorResolver actors, ILogger<OperationsApiController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiOperation>> Create([FromBody] ApiOperationRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return Problem(statusCode: 400, detail: "type and idempotencyKey are required.");
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = request.Type.Trim().ToUpperInvariant() switch
        {
            "SALARYGENERATION" when request.SalaryDefinitionId.HasValue && request.AsOfDate.HasValue => await approvedOperations.RequestSalaryGenerationAsync(actor.Value!.User.Id, request.SalaryDefinitionId.Value, request.AsOfDate.Value, request.IdempotencyKey, ct),
            "OUTBOXWAKEUP" => await approvedOperations.RequestOutboxWakeUpAsync(actor.Value!.User.Id, request.BatchSize, request.IdempotencyKey, ct),
            _ => null
        };
        if (result is null) return Problem(statusCode: 400, detail: "Supported types are SalaryGeneration (salaryDefinitionId and asOfDate required) and OutboxWakeUp.");
        if (result.IsFailure) return Problem(statusCode: 403, detail: result.Error);
        logger.LogInformation("API operation {OperationType} requested by {ActorId}", request.Type, actor.Value!.User.Id);
        return Accepted(ApiOperation.From(result.Value!));
    }
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
public sealed record ApiOperationRequest(string Type, string IdempotencyKey, Guid? SalaryDefinitionId, DateOnly? AsOfDate, int? BatchSize);
