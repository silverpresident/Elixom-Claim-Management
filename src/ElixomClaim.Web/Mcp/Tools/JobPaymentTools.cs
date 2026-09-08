using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListJobPaymentsRequest(JobPaymentStatus? StatusFilter = null);
public sealed record GetJobPaymentRequest(Guid JobPaymentId);

public sealed record JobPaymentDto(
    Guid Id,
    Guid? PayeeUserId,
    Guid? CollectionClientId,
    JobPaymentStatus Status,
    decimal JobTotal,
    decimal ClientProcessingFee,
    decimal TotalTxnProcessingFee,
    decimal TotalDeductions,
    decimal TotalPaid,
    string? PublicNote,
    string? PaymentTransactionNumberRedacted,
    DateTime CreatedAtUtc);

public sealed record JobPaymentListResponse(bool Success, string? Error, List<JobPaymentDto>? JobPayments);
public sealed record JobPaymentDetailResponse(bool Success, string? Error, JobPaymentDto? JobPayment);

[McpServerToolType]
public sealed class JobPaymentTools
{
    private readonly IJobPaymentService _jobs;
    private readonly McpToolActorAccessor _actorAccessor;
    private readonly ILogger<JobPaymentTools> _logger;

    public JobPaymentTools(IJobPaymentService jobs, McpToolActorAccessor actorAccessor, ILogger<JobPaymentTools>? logger = null)
    {
        _jobs = jobs;
        _actorAccessor = actorAccessor;
        _logger = logger ?? NullLogger<JobPaymentTools>.Instance;
    }

    [McpServerTool(Name = "job_payments_list"), Description("List job payments visible to the authenticated user.")]
    public async Task<JobPaymentListResponse> ListJobPayments(ListJobPaymentsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await ListJobPaymentsAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP job payment list completed for actor {ActorId} with success {Success}", actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_JOB_PAYMENTS_LIST", "JobPayments", cancellationToken);
        return response;
    }

    [McpServerTool(Name = "job_payments_get"), Description("Get a job payment visible to the authenticated user.")]
    public async Task<JobPaymentDetailResponse> GetJobPayment(GetJobPaymentRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await GetJobPaymentAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP job payment {JobPaymentId} read by {ActorId} with success {Success}", request.JobPaymentId, actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_JOB_PAYMENTS_GET", $"JobPayment:{request.JobPaymentId}", cancellationToken);
        return response;
    }

    public async Task<JobPaymentListResponse> ListJobPaymentsAsync(User actor, ListJobPaymentsRequest request, CancellationToken ct)
    {
        var result = await _jobs.ListForActorAsync(actor.Id, request.StatusFilter, 100, ct);
        return result.IsSuccess ? new(true, null, result.Value!.Select(ToDto).ToList()) : new(false, result.Error, null);
    }

    public async Task<JobPaymentDetailResponse> GetJobPaymentAsync(User actor, GetJobPaymentRequest request, CancellationToken ct)
    {
        var result = await _jobs.GetForActorAsync(actor.Id, request.JobPaymentId, ct);
        return result.IsSuccess ? new(true, null, ToDto(result.Value!)) : new(false, result.Error, null);
    }
    private static JobPaymentDto ToDto(JobPaymentReadModel job) => new(job.Id, job.PayeeUserId, job.CollectionClientId, job.Status, job.JobTotal, job.ClientProcessingFee, job.TotalTxnProcessingFee, job.TotalDeductions, job.TotalPaid, job.PublicNote, job.PaymentTransactionNumber, job.CreatedAtUtc);
}
