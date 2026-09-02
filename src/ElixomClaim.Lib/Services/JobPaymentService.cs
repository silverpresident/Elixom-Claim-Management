using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class JobPaymentService : IJobPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ISystemClock _clock;
    private readonly ILogger<JobPaymentService> _logger;
    public JobPaymentService(ApplicationDbContext db, IAuditService audit, ISystemClock clock, ILogger<JobPaymentService> logger) { _db = db; _audit = audit; _clock = clock; _logger = logger; }

    public async Task<Result<JobPayment>> CreateAsync(CreateJobPaymentCommand c, CancellationToken ct = default)
    {
        var auth = await AuthorizeAsync(c.ActorUserId, ct); if (auth.IsFailure) return Result.Failure<JobPayment>(auth.Error);
        if ((c.PayeeUserId.HasValue) == (c.CollectionClientId.HasValue)) return Result.Failure<JobPayment>("Choose exactly one payee: a user or a collection client.");
        if (c.PayeeUserId.HasValue && !await _db.Users.AnyAsync(u => u.Id == c.PayeeUserId && u.IsActive, ct)) return Result.Failure<JobPayment>("Payee user was not found.");
        if (c.CollectionClientId.HasValue && !await _db.CollectionClients.AnyAsync(x => x.Id == c.CollectionClientId && x.IsActive, ct)) return Result.Failure<JobPayment>("Collection client was not found.");
        var job = new JobPayment { PayeeUserId = c.PayeeUserId, CollectionClientId = c.CollectionClientId, PublicNote = Trim(c.PublicNote), InternalNote = Trim(c.InternalNote), CreatedAtUtc = _clock.UtcNow };
        _db.JobPayments.Add(job); await _db.SaveChangesAsync(ct); await AuditAsync("JOB_PAYMENT_CREATED", job, c.ActorUserId, ct); return Result.Success(job);
    }

    public async Task<Result> AttachClaimAsync(AttachJobPaymentClaimCommand c, CancellationToken ct = default)
    {
        var jobResult = await ProcessingJobAsync(c.ActorUserId, c.JobPaymentId, ct); if (jobResult.IsFailure) return Result.Failure(jobResult.Error); var job = jobResult.Value!;
        var claim = await _db.Claims.SingleOrDefaultAsync(x => x.Id == c.ClaimId, ct);
        if (claim is null || claim.Status != ClaimStatus.Accepted || claim.ClaimantUserId != job.PayeeUserId) return Result.Failure("Only an accepted claim for the job's user payee can be attached.");
        if (await _db.JobPaymentClaims.AnyAsync(x => x.ClaimId == c.ClaimId, ct)) return Result.Failure("Claim is already attached to a job.");
        _db.JobPaymentClaims.Add(new() { JobPaymentId = job.Id, ClaimId = claim.Id }); claim.PaymentStatus = ClaimPaymentStatus.Processing; await _db.SaveChangesAsync(ct); await RecalculateAsync(job, ct); await _db.SaveChangesAsync(ct); await AuditAsync("JOB_PAYMENT_CLAIM_ATTACHED", job, c.ActorUserId, ct); return Result.Success();
    }

    public async Task<Result> AttachCollectionAsync(AttachJobPaymentCollectionCommand c, CancellationToken ct = default)
    {
        var jobResult = await ProcessingJobAsync(c.ActorUserId, c.JobPaymentId, ct); if (jobResult.IsFailure) return Result.Failure(jobResult.Error); var job = jobResult.Value!;
        var collection = await _db.CollectionTransactions.SingleOrDefaultAsync(x => x.Id == c.CollectionTransactionId, ct);
        if (collection is null || collection.Status != CollectionStatus.Collected || collection.CollectionClientId != job.CollectionClientId) return Result.Failure("Only a Collected transaction for the job's client can be attached.");
        if (await _db.JobPaymentCollections.AnyAsync(x => x.CollectionTransactionId == c.CollectionTransactionId, ct)) return Result.Failure("Collection is already attached to a job.");
        _db.JobPaymentCollections.Add(new() { JobPaymentId = job.Id, CollectionTransactionId = collection.Id }); collection.Status = CollectionStatus.Processing; await _db.SaveChangesAsync(ct); await RecalculateAsync(job, ct); await _db.SaveChangesAsync(ct); await AuditAsync("JOB_PAYMENT_COLLECTION_ATTACHED", job, c.ActorUserId, ct); return Result.Success();
    }

    public async Task<Result> RemoveClaimAsync(RemoveJobPaymentClaimCommand c, CancellationToken ct = default)
    {
        var jobResult = await ProcessingJobAsync(c.ActorUserId, c.JobPaymentId, ct); if (jobResult.IsFailure) return Result.Failure(jobResult.Error); var line = await _db.JobPaymentClaims.Include(x => x.Claim).SingleOrDefaultAsync(x => x.JobPaymentId == c.JobPaymentId && x.ClaimId == c.ClaimId, ct); if (line is null) return Result.Failure("Attached claim was not found.");
        line.Claim.PaymentStatus = ClaimPaymentStatus.Unpaid; _db.JobPaymentClaims.Remove(line); await _db.SaveChangesAsync(ct); await RecalculateAsync(jobResult.Value!, ct); await _db.SaveChangesAsync(ct); return Result.Success();
    }
    public async Task<Result> RemoveCollectionAsync(RemoveJobPaymentCollectionCommand c, CancellationToken ct = default)
    {
        var jobResult = await ProcessingJobAsync(c.ActorUserId, c.JobPaymentId, ct); if (jobResult.IsFailure) return Result.Failure(jobResult.Error); var line = await _db.JobPaymentCollections.Include(x => x.CollectionTransaction).SingleOrDefaultAsync(x => x.JobPaymentId == c.JobPaymentId && x.CollectionTransactionId == c.CollectionTransactionId, ct); if (line is null) return Result.Failure("Attached collection was not found.");
        line.CollectionTransaction.Status = CollectionStatus.Collected; _db.JobPaymentCollections.Remove(line); await _db.SaveChangesAsync(ct); await RecalculateAsync(jobResult.Value!, ct); await _db.SaveChangesAsync(ct); return Result.Success();
    }
    public async Task<Result> AddDeductionAsync(AddJobPaymentDeductionCommand c, CancellationToken ct = default)
    {
        var jobResult = await ProcessingJobAsync(c.ActorUserId, c.JobPaymentId, ct); if (jobResult.IsFailure) return Result.Failure(jobResult.Error); if (string.IsNullOrWhiteSpace(c.Description) || c.Amount <= 0) return Result.Failure("A deduction description and positive amount are required.");
        _db.JobPaymentDeductions.Add(new() { JobPaymentId = c.JobPaymentId, Description = c.Description.Trim(), Amount = c.Amount, CreatedAtUtc = _clock.UtcNow }); await _db.SaveChangesAsync(ct); await RecalculateAsync(jobResult.Value!, ct); await _db.SaveChangesAsync(ct); return Result.Success();
    }

    public async Task<Result> ResendNotificationAsync(long jobPaymentId, Guid actorUserId, CancellationToken ct = default)
    {
        var auth = await AuthorizeAsync(actorUserId, ct); if (auth.IsFailure) return auth;
        var job = await _db.JobPayments.SingleOrDefaultAsync(j => j.Id == jobPaymentId, ct);
        if (job is null || job.Status != JobPaymentStatus.Paid) return Result.Failure("Only a paid job payment can have its payout notification resent.");
        var originals = await _db.EmailOutboxItems.Where(e => e.RelatedEntityType == "JobPayment" && e.RelatedEntityId == job.Id.ToString() && e.Status != EmailOutboxStatus.SkippedInvalidRecipient).ToListAsync(ct);
        if (originals.Count == 0) return Result.Failure("No previously authorized payout notification is available to resend.");
        foreach (var original in originals)
            _db.EmailOutboxItems.Add(new EmailOutboxItem { Recipient = original.Recipient, Subject = original.Subject, HtmlBody = original.HtmlBody, RelatedEntityType = original.RelatedEntityType, RelatedEntityId = original.RelatedEntityId, IdempotencyKey = $"job-payment-resend:{job.Id}:{Guid.NewGuid():N}", Status = EmailOutboxStatus.Pending, AvailableAtUtc = _clock.UtcNow, CreatedAtUtc = _clock.UtcNow });
        await _db.SaveChangesAsync(ct); await AuditAsync("JOB_PAYMENT_NOTIFICATION_RESEND_QUEUED", job, actorUserId, ct); return Result.Success();
    }

    private async Task RecalculateAsync(JobPayment job, CancellationToken ct)
    {
        var claims = await _db.JobPaymentClaims.Where(x => x.JobPaymentId == job.Id).Select(x => x.Claim.Amount).ToListAsync(ct); var collections = await _db.JobPaymentCollections.Where(x => x.JobPaymentId == job.Id).Select(x => new { x.CollectionTransaction.Amount, x.CollectionTransaction.ProcessingFee }).ToListAsync(ct); var payrolls = await _db.JobPaymentPayrolls.Where(x => x.JobPaymentId == job.Id).Select(x => x.Payroll.NetAmount).ToListAsync(ct); var deductions = await _db.JobPaymentDeductions.Where(x => x.JobPaymentId == job.Id).Select(x => x.Amount).ToListAsync(ct);
        job.JobTotal = claims.Sum() + collections.Sum(x => x.Amount) + payrolls.Sum(); job.ClientProcessingFee = collections.Sum(x => x.ProcessingFee); job.TotalTxnProcessingFee = 0m; job.TotalDeductions = deductions.Sum(); job.TotalPaid = job.JobTotal - job.ClientProcessingFee - job.TotalTxnProcessingFee - job.TotalDeductions;
    }
    private async Task<Result<JobPayment>> ProcessingJobAsync(Guid actor, long id, CancellationToken ct) { var auth = await AuthorizeAsync(actor, ct); if (auth.IsFailure) return Result.Failure<JobPayment>(auth.Error); var job = await _db.JobPayments.SingleOrDefaultAsync(j => j.Id == id, ct); return job is null ? Result.Failure<JobPayment>("Job payment was not found.") : job.Status != JobPaymentStatus.Processing ? Result.Failure<JobPayment>("Only Processing job payments can be changed.") : Result.Success(job); }
    private async Task<Result> AuthorizeAsync(Guid actor, CancellationToken ct) { var role = await _db.Users.Where(u => u.Id == actor && u.IsActive).Select(u => (UserRole?)u.Role).SingleOrDefaultAsync(ct); return role is { } r && r.HasMinimumRole(UserRole.Manager) ? Result.Success() : Result.Failure("Manager access is required."); }
    private Task AuditAsync(string action, JobPayment job, Guid actor, CancellationToken ct) => _audit.LogAsync(action, $"JobPayment:{job.Id}", afterState: new { job.Id, job.Status, job.JobTotal, job.TotalPaid }, actorUserId: actor.ToString(), cancellationToken: ct);
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
