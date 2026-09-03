using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListJobPaymentsRequest(JobPaymentStatus? StatusFilter = null);
public sealed record GetJobPaymentRequest(long JobPaymentId);

public sealed record JobPaymentDto(
    long Id,
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

public sealed class JobPaymentTools
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _audit;

    public JobPaymentTools(ApplicationDbContext dbContext, IAuditService audit)
    {
        _dbContext = dbContext;
        _audit = audit;
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
        catch (Exception ex)
        {
            return new JobPaymentListResponse(false, ex.Message, null);
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
        catch (Exception ex)
        {
            return new JobPaymentDetailResponse(false, ex.Message, null);
        }
    }

    private static string? RedactTxnNumber(string? txnNumber)
    {
        if (string.IsNullOrEmpty(txnNumber)) return null;
        if (txnNumber.Length <= 4) return "****";
        return "****" + txnNumber[^4..];
    }
}
