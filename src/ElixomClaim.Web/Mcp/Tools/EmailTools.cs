using System.Net.Mail;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record EmailPreviewRequest(string TemplateType, long EntityId);
public sealed record EmailQueueSendRequest(string TemplateType, long EntityId, string IdempotencyKey);

public sealed record EmailPreviewResponse(
    bool Success,
    string? Error,
    string? Subject,
    string? RedactedHtmlBody,
    List<string>? RecipientSummary);

public sealed record EmailQueueSendResponse(
    bool Success,
    string? Error,
    int QueuedCount);

public sealed class EmailTools
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly ISystemClock _clock;
    private readonly NotificationOptions _notificationOptions;

    public EmailTools(
        ApplicationDbContext dbContext,
        IAuditService audit,
        ISystemClock clock,
        IOptions<NotificationOptions> notificationOptions)
    {
        _dbContext = dbContext;
        _audit = audit;
        _clock = clock;
        _notificationOptions = notificationOptions.Value;
    }

    public async Task<EmailPreviewResponse> PreviewAsync(User actor, EmailPreviewRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateType))
        {
            return new EmailPreviewResponse(false, "TemplateType is required.", null, null, null);
        }

        var templateType = request.TemplateType.Trim();

        if (string.Equals(templateType, "CollectionReceipt", StringComparison.OrdinalIgnoreCase))
        {
            return await PreviewCollectionReceiptAsync(actor, request.EntityId, ct);
        }
        else if (string.Equals(templateType, "PaymentSummary", StringComparison.OrdinalIgnoreCase))
        {
            return await PreviewPaymentSummaryAsync(actor, request.EntityId, ct);
        }
        else
        {
            return new EmailPreviewResponse(false, $"Unsupported or prohibited email template type '{request.TemplateType}'. Direct free-form email composition is prohibited.", null, null, null);
        }
    }

    public async Task<EmailQueueSendResponse> QueueSendAsync(User actor, EmailQueueSendRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateType) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new EmailQueueSendResponse(false, "TemplateType and IdempotencyKey are required.", 0);
        }

        var templateType = request.TemplateType.Trim();

        if (string.Equals(templateType, "CollectionReceipt", StringComparison.OrdinalIgnoreCase))
        {
            return await QueueCollectionReceiptSendAsync(actor, request.EntityId, request.IdempotencyKey.Trim(), ct);
        }
        else if (string.Equals(templateType, "PaymentSummary", StringComparison.OrdinalIgnoreCase))
        {
            return await QueuePaymentSummarySendAsync(actor, request.EntityId, request.IdempotencyKey.Trim(), ct);
        }
        else
        {
            return new EmailQueueSendResponse(false, $"Unsupported or prohibited email template type '{request.TemplateType}'. Direct free-form email send is prohibited.", 0);
        }
    }

    private async Task<EmailPreviewResponse> PreviewCollectionReceiptAsync(User actor, long collectionId, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new EmailPreviewResponse(false, "Access denied. Teller role or higher required for receipt template preview.", null, null, null);
        }

        var collection = await _dbContext.CollectionTransactions
            .Include(c => c.CollectionClient)
            .Include(c => c.PurposeOption)
            .Include(c => c.AmountOption)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null)
        {
            return new EmailPreviewResponse(false, "Collection record not found.", null, null, null);
        }

        var html = ComposeReceiptHtml(collection, collection.CollectionClient, collection.PurposeOption, collection.AmountOption);
        var subject = $"Collection receipt #{collection.Id}";

        var recipients = new List<string>();
        if (!string.IsNullOrWhiteSpace(collection.PayorEmail)) recipients.Add(RedactEmail(collection.PayorEmail));
        if (!string.IsNullOrWhiteSpace(_notificationOptions.SystemCopyAddress)) recipients.Add(RedactEmail(_notificationOptions.SystemCopyAddress));

        var clientUsers = await _dbContext.CollectionClientUsers
            .Where(a => a.CollectionClientId == collection.CollectionClientId && a.User.IsActive)
            .Select(a => a.User.Email)
            .ToListAsync(ct);

        foreach (var cu in clientUsers)
        {
            if (!string.IsNullOrWhiteSpace(cu)) recipients.Add(RedactEmail(cu));
        }

        await _audit.LogAsync("MCP_EMAIL_PREVIEW", $"CollectionReceipt:{collectionId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
        return new EmailPreviewResponse(true, null, subject, html, recipients.Distinct().ToList());
    }

    private async Task<EmailPreviewResponse> PreviewPaymentSummaryAsync(User actor, long jobPaymentId, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Manager))
        {
            return new EmailPreviewResponse(false, "Access denied. Manager role or higher required for payout summary preview.", null, null, null);
        }

        var job = await _dbContext.JobPayments
            .Include(j => j.Claims).ThenInclude(x => x.Claim)
            .Include(j => j.Collections).ThenInclude(x => x.CollectionTransaction)
            .Include(j => j.Deductions)
            .Include(j => j.PayeeUser)
            .FirstOrDefaultAsync(j => j.Id == jobPaymentId, ct);

        if (job == null)
        {
            return new EmailPreviewResponse(false, "Job payment record not found.", null, null, null);
        }

        var html = ComposePayoutHtml(job, actor.Role.HasMinimumRole(UserRole.Accountant));
        var subject = $"Payout summary #{job.Id}";

        var recipients = new List<string>();
        if (job.PayeeUser != null && !string.IsNullOrWhiteSpace(job.PayeeUser.Email))
        {
            recipients.Add(RedactEmail(job.PayeeUser.Email));
        }
        else if (job.CollectionClientId.HasValue)
        {
            var clientUsers = await _dbContext.CollectionClientUsers
                .Where(x => x.CollectionClientId == job.CollectionClientId.Value && x.User.IsActive)
                .Select(x => x.User.Email)
                .ToListAsync(ct);
            foreach (var cu in clientUsers)
            {
                if (!string.IsNullOrWhiteSpace(cu)) recipients.Add(RedactEmail(cu));
            }
        }

        await _audit.LogAsync("MCP_EMAIL_PREVIEW", $"PaymentSummary:{jobPaymentId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
        return new EmailPreviewResponse(true, null, subject, html, recipients.Distinct().ToList());
    }

    private async Task<EmailQueueSendResponse> QueueCollectionReceiptSendAsync(User actor, long collectionId, string idempotencyKey, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new EmailQueueSendResponse(false, "Access denied. Teller role or higher required.", 0);
        }

        var collection = await _dbContext.CollectionTransactions
            .Include(c => c.CollectionClient)
            .Include(c => c.PurposeOption)
            .Include(c => c.AmountOption)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null)
        {
            return new EmailQueueSendResponse(false, "Collection record not found.", 0);
        }

        // Idempotency check
        var existingKey = $"mcp-collection-receipt:{collectionId}:{idempotencyKey}";
        if (await _dbContext.EmailOutboxItems.AnyAsync(e => e.IdempotencyKey == existingKey, ct))
        {
            return new EmailQueueSendResponse(true, "Request already processed (idempotent).", 0);
        }

        var clientUsers = await _dbContext.CollectionClientUsers
            .Where(a => a.CollectionClientId == collection.CollectionClientId && a.User.IsActive)
            .Select(a => a.User.Email)
            .ToListAsync(ct);

        var recipients = new[] { collection.PayorEmail, _notificationOptions.SystemCopyAddress }
            .Concat(clientUsers)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int queuedCount = 0;
        var html = ComposeReceiptHtml(collection, collection.CollectionClient, collection.PurposeOption, collection.AmountOption);
        var subject = $"Collection receipt #{collection.Id}";

        foreach (var recipient in recipients)
        {
            var valid = IsValidEmail(recipient!);
            var itemKey = $"{existingKey}:{recipient!.ToUpperInvariant()}";
            if (!await _dbContext.EmailOutboxItems.AnyAsync(e => e.IdempotencyKey == itemKey, ct))
            {
                _dbContext.EmailOutboxItems.Add(new EmailOutboxItem
                {
                    Recipient = recipient!,
                    Subject = subject,
                    HtmlBody = html,
                    RelatedEntityType = "CollectionTransaction",
                    RelatedEntityId = collection.Id.ToString(),
                    IdempotencyKey = itemKey,
                    Status = valid ? EmailOutboxStatus.Pending : EmailOutboxStatus.SkippedInvalidRecipient,
                    FailureReason = valid ? null : "Invalid recipient address.",
                    AvailableAtUtc = _clock.UtcNow,
                    CreatedAtUtc = _clock.UtcNow
                });
                queuedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await _audit.LogAsync("MCP_EMAIL_QUEUE_SEND", $"CollectionReceipt:{collectionId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
        return new EmailQueueSendResponse(true, null, queuedCount);
    }

    private async Task<EmailQueueSendResponse> QueuePaymentSummarySendAsync(User actor, long jobPaymentId, string idempotencyKey, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Accountant))
        {
            return new EmailQueueSendResponse(false, "Access denied. Accountant role required to queue payment summaries via MCP.", 0);
        }

        var job = await _dbContext.JobPayments
            .Include(j => j.Claims).ThenInclude(x => x.Claim)
            .Include(j => j.Collections).ThenInclude(x => x.CollectionTransaction)
            .Include(j => j.Deductions)
            .Include(j => j.PayeeUser)
            .FirstOrDefaultAsync(j => j.Id == jobPaymentId, ct);

        if (job == null)
        {
            return new EmailQueueSendResponse(false, "Job payment record not found.", 0);
        }

        if (job.Status != JobPaymentStatus.Paid && job.Status != JobPaymentStatus.Scheduled)
        {
            return new EmailQueueSendResponse(false, "Payment summary can only be queued for Scheduled or Paid job payments.", 0);
        }

        var existingKey = $"mcp-job-payment-summary:{jobPaymentId}:{idempotencyKey}";
        if (await _dbContext.EmailOutboxItems.AnyAsync(e => e.IdempotencyKey == existingKey, ct))
        {
            return new EmailQueueSendResponse(true, "Request already processed (idempotent).", 0);
        }

        var recipients = job.PayeeUser != null
            ? new[] { job.PayeeUser.Email }
            : await _dbContext.CollectionClientUsers.Where(x => x.CollectionClientId == job.CollectionClientId && x.User.IsActive).Select(x => x.User.Email).ToArrayAsync(ct);

        int queuedCount = 0;
        var html = ComposePayoutHtml(job, canViewFullBankDetails: true);
        var subject = $"Payout summary #{job.Id}";

        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var valid = IsValidEmail(recipient!);
            var itemKey = $"{existingKey}:{recipient!.ToUpperInvariant()}";
            if (!await _dbContext.EmailOutboxItems.AnyAsync(e => e.IdempotencyKey == itemKey, ct))
            {
                _dbContext.EmailOutboxItems.Add(new EmailOutboxItem
                {
                    Recipient = recipient!,
                    Subject = subject,
                    HtmlBody = html,
                    RelatedEntityType = "JobPayment",
                    RelatedEntityId = job.Id.ToString(),
                    IdempotencyKey = itemKey,
                    Status = valid ? EmailOutboxStatus.Pending : EmailOutboxStatus.SkippedInvalidRecipient,
                    FailureReason = valid ? null : "Invalid recipient address.",
                    AvailableAtUtc = _clock.UtcNow,
                    CreatedAtUtc = _clock.UtcNow
                });
                queuedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await _audit.LogAsync("MCP_EMAIL_QUEUE_SEND", $"PaymentSummary:{jobPaymentId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
        return new EmailQueueSendResponse(true, null, queuedCount);
    }

    private static string ComposeReceiptHtml(CollectionTransaction collection, CollectionClient client, CollectionPurposeOption purpose, CollectionAmountOption amount) =>
        $"<article><h1>Collection receipt</h1><p>Receipt #{collection.Id}</p><dl><dt>Client</dt><dd>{HtmlEncoder.Default.Encode(client.Name)}</dd><dt>Purpose</dt><dd>{HtmlEncoder.Default.Encode(purpose.Name)}</dd><dt>Amount</dt><dd>{amount.Amount:N2} JMD</dd><dt>Payment date (UTC)</dt><dd>{collection.PaymentDateUtc:yyyy-MM-dd HH:mm}</dd><dt>Method</dt><dd>{collection.Method}</dd></dl></article>";

    private static string ComposePayoutHtml(JobPayment job, bool canViewFullBankDetails)
    {
        Func<string, string> encode = HtmlEncoder.Default.Encode;
        var claims = string.Join("", job.Claims.Select(x => $"<li>{encode(x.Claim.Title)} — {x.Claim.Amount:N2} JMD</li>"));
        var collections = string.Join("", job.Collections.Select(x => $"<li>Collection #{x.CollectionTransactionId} — {x.CollectionTransaction.Amount:N2} JMD</li>"));
        var deductions = string.Join("", job.Deductions.Select(x => $"<li>{encode(x.Description)} — {x.Amount:N2} JMD</li>"));

        string bankInfo;
        if (job.PayeeUser != null)
        {
            var rawAccount = job.PayeeUser.BankAccountNumber;
            var accountDisplay = canViewFullBankDetails
                ? encode(rawAccount ?? "unavailable")
                : encode(rawAccount != null && rawAccount.Length > 4 ? "****" + rawAccount[^4..] : "****");
            bankInfo = $"Bank account ending {accountDisplay}";
        }
        else
        {
            bankInfo = "Collection client payout";
        }

        return $"<article style=\"font-family:Arial,sans-serif;max-width:720px;margin:auto\"><h1>Payout summary</h1><p>Payment #{job.Id}</p><p>{bankInfo}</p><p>Payment date: {job.PaymentDateUtc:yyyy-MM-dd} UTC<br/>Transaction: {encode(canViewFullBankDetails ? (job.PaymentTransactionNumber ?? string.Empty) : (job.PaymentTransactionNumber != null && job.PaymentTransactionNumber.Length > 4 ? "****" + job.PaymentTransactionNumber[^4..] : "****"))}</p><h2>Claims</h2><ul>{claims}</ul><h2>Collections</h2><ul>{collections}</ul><h2>Deductions</h2><ul>{deductions}</ul><table><tr><th>Job total</th><td>{job.JobTotal:N2} JMD</td></tr><tr><th>Client fee</th><td>{job.ClientProcessingFee:N2} JMD</td></tr><tr><th>Deductions</th><td>{job.TotalDeductions:N2} JMD</td></tr><tr><th>Total paid</th><td><strong>{job.TotalPaid:N2} JMD</strong></td></tr></table></article>";
    }

    private static string RedactEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var parts = email.Split('@');
        if (parts.Length != 2) return "***";
        var name = parts[0];
        var domain = parts[1];
        var redactedName = name.Length <= 2 ? name[0] + "*" : name[0] + new string('*', name.Length - 2) + name[^1];
        return $"{redactedName}@{domain}";
    }

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
}
