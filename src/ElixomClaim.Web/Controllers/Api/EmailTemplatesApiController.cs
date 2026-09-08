using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController, Route("api/v1/email-templates"), Authorize(Policy = "ApiAccess")]
public sealed class EmailTemplatesApiController(ICollectionService collections, IJobPaymentService jobPayments, IApprovedEmailPreviewService previews, IActorResolver actors, ILogger<EmailTemplatesApiController> logger) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<ApiEmailPreviewResult>> Preview([FromBody] ApiEmailPreviewRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TemplateType)) return Problem(statusCode: 400, detail: "templateType and entityId are required.");
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = await previews.PreviewAsync(actor.Value!.User.Id, request.TemplateType, request.EntityId, ct);
        if (result.IsFailure) return Problem(statusCode: 403, detail: result.Error);
        logger.LogInformation("API approved email preview {TemplateType} requested by {ActorId}", request.TemplateType, actor.Value.User.Id);
        return Ok(new ApiEmailPreviewResult(result.Value!.Subject, result.Value.RedactedHtmlBody, result.Value.RecipientSummary));
    }
    [HttpPost("queue")]
    public async Task<ActionResult<ApiEmailQueueResult>> Queue([FromBody] ApiEmailQueueRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TemplateType) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "templateType, entityId, and idempotencyKey are required.");
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = request.TemplateType.Trim().ToUpperInvariant() switch
        {
            "COLLECTIONRECEIPT" => await collections.QueueReceiptAsync(request.EntityId, actor.Value!.User.Id, request.IdempotencyKey, ct),
            "PAYMENTSUMMARY" => await jobPayments.QueuePaymentSummaryAsync(request.EntityId, actor.Value!.User.Id, request.IdempotencyKey, ct),
            _ => null
        };
        if (result is null) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Only CollectionReceipt and PaymentSummary are approved templates.");
        if (result.IsFailure) return Problem(statusCode: StatusCodes.Status403Forbidden, detail: result.Error);
        logger.LogInformation("API approved email queue {TemplateType} requested by {ActorId} with queued count {QueuedCount}", request.TemplateType, actor.Value!.User.Id, result.Value!);
        return Accepted(new ApiEmailQueueResult(result.Value!, result.Value == 0));
    }
}
public sealed record ApiEmailQueueRequest(string TemplateType, Guid EntityId, string IdempotencyKey);
public sealed record ApiEmailQueueResult(int QueuedCount, bool WasIdempotent);
public sealed record ApiEmailPreviewRequest(string TemplateType, Guid EntityId);
public sealed record ApiEmailPreviewResult(string Subject, string RedactedHtmlBody, IReadOnlyList<string> RecipientSummary);
