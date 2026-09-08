using System.Text.Encodings.Web;
using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Lib.Services;

public sealed class ApprovedEmailPreviewService(ApplicationDbContext db, IOptions<NotificationOptions> notifications, IAuditService audit) : IApprovedEmailPreviewService
{
    public async Task<Result<ApprovedEmailPreview>> PreviewAsync(Guid actorUserId, string templateType, Guid entityId, CancellationToken ct = default)
    {
        if (!string.Equals(templateType, "CollectionReceipt", StringComparison.OrdinalIgnoreCase) && !string.Equals(templateType, "PaymentSummary", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ApprovedEmailPreview>("Unsupported or prohibited email template. Only CollectionReceipt and PaymentSummary are approved.");
        var role = await db.Users.Where(user => user.Id == actorUserId && user.IsActive).Select(user => (UserRole?)user.Role).SingleOrDefaultAsync(ct);
        if (role is null) return Result.Failure<ApprovedEmailPreview>("Approved template preview is not available.");
        if (string.Equals(templateType, "CollectionReceipt", StringComparison.OrdinalIgnoreCase))
        {
            if (!role.Value.HasMinimumRole(UserRole.Teller)) return Result.Failure<ApprovedEmailPreview>("Teller access is required.");
            var collectionQuery = db.CollectionTransactions
                .Include(item => item.CollectionClient)
                .Where(item => item.Id == entityId);
            // Tellers may re-preview only receipts they recorded. Management roles retain
            // their operational-review access, consistent with the collection workspace.
            if (!role.Value.HasMinimumRole(UserRole.Manager))
                collectionQuery = collectionQuery.Where(item => item.TellerUserId == actorUserId);
            var collection = await collectionQuery.SingleOrDefaultAsync(ct);
            if (collection is null) return Result.Failure<ApprovedEmailPreview>("Collection record was not found or is not available.");
            var recipients = new[] { collection.PayorEmail, notifications.Value.SystemCopyAddress }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(RedactEmail).ToList();
            var clientUsers = await db.CollectionClientUsers.Where(item => item.CollectionClientId == collection.CollectionClientId && item.User.IsActive).Select(item => item.User.Email).ToListAsync(ct);
            recipients.AddRange(clientUsers.Where(value => !string.IsNullOrWhiteSpace(value)).Select(RedactEmail));
            await audit.LogAsync("EMAIL_TEMPLATE_PREVIEW", $"CollectionReceipt:{entityId}", actorUserId: actorUserId.ToString(), cancellationToken: ct);
            return Result.Success(new ApprovedEmailPreview($"Collection receipt #{collection.SequenceNo}", $"<article><h1>Collection receipt</h1><p>Receipt #{collection.SequenceNo}</p><p>Client: {HtmlEncoder.Default.Encode(collection.CollectionClient.Name)}</p><p>Amount: {collection.Amount:N2} JMD</p></article>", recipients.Distinct().ToList()));
        }
        if (string.Equals(templateType, "PaymentSummary", StringComparison.OrdinalIgnoreCase))
        {
            if (!role.Value.HasMinimumRole(UserRole.Manager)) return Result.Failure<ApprovedEmailPreview>("Manager access is required.");
            var jobQuery = db.JobPayments.Include(item => item.PayeeUser).Where(item => item.Id == entityId);
            // Managers may preview their own payout only. Accountant and Administrator
            // roles need wider access to reconcile and settle payment operations.
            if (!role.Value.HasMinimumRole(UserRole.Accountant))
                jobQuery = jobQuery.Where(item => item.PayeeUserId == actorUserId);
            var job = await jobQuery.SingleOrDefaultAsync(ct);
            if (job is null) return Result.Failure<ApprovedEmailPreview>("Job payment record was not found or is not available.");
            var recipients = job.PayeeUser is null ? [] : new[] { RedactEmail(job.PayeeUser.Email) };
            await audit.LogAsync("EMAIL_TEMPLATE_PREVIEW", $"PaymentSummary:{entityId}", actorUserId: actorUserId.ToString(), cancellationToken: ct);
            return Result.Success(new ApprovedEmailPreview($"Payout summary #{job.SequenceNo}", $"<article><h1>Payout summary</h1><p>Payment #{job.SequenceNo}</p><p>Total paid: {job.TotalPaid:N2} JMD</p></article>", recipients));
        }
        return Result.Failure<ApprovedEmailPreview>("Unsupported or prohibited email template. Only CollectionReceipt and PaymentSummary are approved.");
    }

    private static string RedactEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var parts = email.Split('@'); if (parts.Length != 2 || parts[0].Length == 0) return "***";
        var local = parts[0];
        return local.Length <= 2 ? $"{local[0]}*@{parts[1]}" : $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}@{parts[1]}";
    }
}
