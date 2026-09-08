using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers.Api;

[ApiController, Route("api/v1/job-payments"), Authorize(Policy = "ApiAccess")]
public sealed class JobPaymentsApiController(IJobPaymentService jobs, IActorResolver actors, ILogger<JobPaymentsApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiJobPaymentPage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] JobPaymentStatus? status = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return Problem(statusCode: 400, detail: "page must be positive and pageSize must be between 1 and 100.");
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = await jobs.ListForActorAsync(actor.Value!.User.Id, status, 100, ct); if (result.IsFailure) return Forbid();
        var all = result.Value!; return Ok(new ApiJobPaymentPage(page, pageSize, all.Count, all.Skip((page - 1) * pageSize).Take(pageSize).Select(ApiJobPayment.From).ToList()));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiJobPayment>> Get(Guid id, CancellationToken ct)
    {
        var actor = await actors.ResolveActorAsync(HttpContext, "api:access", false, ct); if (!actor.IsSuccess) return Forbid();
        var result = await jobs.GetForActorAsync(actor.Value!.User.Id, id, ct); if (result.IsFailure) return NotFound();
        logger.LogInformation("API job payment {JobPaymentId} read by {ActorId}", id, actor.Value.User.Id); return Ok(ApiJobPayment.From(result.Value!));
    }
}
public sealed record ApiJobPayment(Guid Id, long SequenceNo, Guid? PayeeUserId, Guid? CollectionClientId, string Status, decimal JobTotal, decimal ClientProcessingFee, decimal TotalTxnProcessingFee, decimal TotalDeductions, decimal TotalPaid, string? PublicNote, string? PaymentTransactionNumber, DateTime CreatedAtUtc) { public static ApiJobPayment From(JobPaymentReadModel value) => new(value.Id, value.SequenceNo, value.PayeeUserId, value.CollectionClientId, value.Status.ToString(), value.JobTotal, value.ClientProcessingFee, value.TotalTxnProcessingFee, value.TotalDeductions, value.TotalPaid, value.PublicNote, value.PaymentTransactionNumber, value.CreatedAtUtc); }
public sealed record ApiJobPaymentPage(int Page, int PageSize, int TotalCount, IReadOnlyList<ApiJobPayment> Items);
