using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

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
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;

    public JobPaymentTools(ApplicationDbContext dbContext, IAuditService audit, McpToolActorAccessor actorAccessor)
    {
        _dbContext = dbContext;
        _audit = audit;
        _actorAccessor = actorAccessor;
    }

    [McpServerTool(Name = "job_payments_list"), Description("List job payments visible to the authenticated user.")]
    public async Task<JobPaymentListResponse> ListJobPayments(ListJobPaymentsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await ListJobPaymentsAsync(actor.Value!.User, request, cancellationToken);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_JOB_PAYMENTS_LIST", "JobPayments", cancellationToken);
        return response;
    }

    [McpServerTool(Name = "job_payments_get"), Description("Get a job payment visible to the authenticated user.")]
    public async Task<JobPaymentDetailResponse> GetJobPayment(GetJobPaymentRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await GetJobPaymentAsync(actor.Value!.User, request, cancellationToken);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_JOB_PAYMENTS_GET", $"JobPayment:{request.JobPaymentId}", cancellationToken);
        return response;
    }

    public async Task<JobPaymentListResponse> ListJobPaymentsAsync(User actor, ListJobPaymentsRequest request, CancellationToken ct)
    {
        try
        {
            var query = _dbContext.JobPayments.AsNoTracking();

            if (!actor.Role.HasMinimumRole(UserRole.Manager))
            {
                // Regular users can only list job payments where they are the payee
                query = query.Where(j => j.PayeeUserId == actor.Id);
            }

            if (request.StatusFilter.HasValue)
            {
                query = query.Where(j => j.Status == request.StatusFilter.Value);
            }

            var jobs = await query.OrderByDescending(j => j.CreatedAtUtc).Take(100).ToListAsync(ct);

            bool canViewSensitive = actor.Role.HasMinimumRole(UserRole.Accountant);

            var dtos = jobs.Select(j => new JobPaymentDto(
                j.Id,
                j.PayeeUserId,
                j.CollectionClientId,
                j.Status,
                j.JobTotal,
                j.ClientProcessingFee,
                j.TotalTxnProcessingFee,
                j.TotalDeductions,
                j.TotalPaid,
                j.PublicNote,
                canViewSensitive ? j.PaymentTransactionNumber : RedactTxnNumber(j.PaymentTransactionNumber),
                j.CreatedAtUtc
            )).ToList();

            await _audit.LogAsync("MCP_JOB_PAYMENTS_LIST", $"Actor:{actor.Id}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new JobPaymentListResponse(true, null, dtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new JobPaymentListResponse(false, "Job payments could not be retrieved.", null);
        }
    }

    public async Task<JobPaymentDetailResponse> GetJobPaymentAsync(User actor, GetJobPaymentRequest request, CancellationToken ct)
    {
        try
        {
            var job = await _dbContext.JobPayments.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobPaymentId, ct);
            if (job == null)
            {
                return new JobPaymentDetailResponse(false, "Job payment not found.", null);
            }

            // Access check
            if (!actor.Role.HasMinimumRole(UserRole.Manager) && job.PayeeUserId != actor.Id)
            {
                return new JobPaymentDetailResponse(false, "Access denied.", null);
            }

            bool canViewSensitive = actor.Role.HasMinimumRole(UserRole.Accountant);

            var dto = new JobPaymentDto(
                job.Id,
                job.PayeeUserId,
                job.CollectionClientId,
                job.Status,
                job.JobTotal,
                job.ClientProcessingFee,
                job.TotalTxnProcessingFee,
                job.TotalDeductions,
                job.TotalPaid,
                job.PublicNote,
                canViewSensitive ? job.PaymentTransactionNumber : RedactTxnNumber(job.PaymentTransactionNumber),
                job.CreatedAtUtc
            );

            await _audit.LogAsync("MCP_JOB_PAYMENT_GET", $"JobPayment:{request.JobPaymentId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new JobPaymentDetailResponse(true, null, dto);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new JobPaymentDetailResponse(false, "The job payment could not be retrieved.", null);
        }
    }

    private static string? RedactTxnNumber(string? txnNumber)
    {
        if (string.IsNullOrEmpty(txnNumber)) return null;
        if (txnNumber.Length <= 4) return "****";
        return "****" + txnNumber[^4..];
    }
}
