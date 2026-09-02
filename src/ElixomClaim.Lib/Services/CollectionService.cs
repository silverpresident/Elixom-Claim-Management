using System.Net.Mail;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Lib.Services;

public class CollectionService : ICollectionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ISystemClock _clock;
    private readonly NotificationOptions _notificationOptions;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(ApplicationDbContext dbContext, IAuditService auditService, ISystemClock clock, IOptions<NotificationOptions> notificationOptions, ILogger<CollectionService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _clock = clock;
        _notificationOptions = notificationOptions.Value;
        _logger = logger;
    }

    public async Task<Result<CollectionTransaction>> RecordAsync(RecordCollectionCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.PayorName) || command.ProcessingFee < 0 || command.PaymentDateUtc.Kind != DateTimeKind.Utc)
            return Result.Failure<CollectionTransaction>("Payor name, a non-negative processing fee, and a UTC payment date are required.");

        var teller = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == command.TellerUserId && u.IsActive, cancellationToken);
        if (teller is null || !teller.Role.HasMinimumRole(UserRole.Teller)) return Result.Failure<CollectionTransaction>("Teller access is required.");
        var client = await _dbContext.CollectionClients.SingleOrDefaultAsync(c => c.Id == command.CollectionClientId && c.IsActive, cancellationToken);
        var purpose = await _dbContext.CollectionPurposeOptions.SingleOrDefaultAsync(o => o.Id == command.PurposeOptionId && o.CollectionClientId == command.CollectionClientId && o.IsActive, cancellationToken);
        var amountOption = await _dbContext.CollectionAmountOptions.SingleOrDefaultAsync(o => o.Id == command.AmountOptionId && o.CollectionClientId == command.CollectionClientId && o.IsActive, cancellationToken);
        if (client is null || purpose is null || amountOption is null) return Result.Failure<CollectionTransaction>("Choose active purpose and amount options belonging to the selected client.");

        await using var transaction = _dbContext.Database.IsRelational() ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var collection = new CollectionTransaction
            {
                CollectionClientId = client.Id,
                PurposeOptionId = purpose.Id,
                AmountOptionId = amountOption.Id,
                TellerUserId = teller.Id,
                PayorName = command.PayorName.Trim(),
                PayorEmail = string.IsNullOrWhiteSpace(command.PayorEmail) ? null : command.PayorEmail.Trim(),
                ReferenceNumber = string.IsNullOrWhiteSpace(command.ReferenceNumber) ? null : command.ReferenceNumber.Trim(),
                Method = command.Method,
                Status = CollectionStatus.Collected,
                Amount = amountOption.Amount,
                ProcessingFee = command.ProcessingFee,
                Currency = "JMD",
                PaymentDateUtc = command.PaymentDateUtc,
                CreatedAtUtc = _clock.UtcNow
            };
            _dbContext.CollectionTransactions.Add(collection);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (collection.PayorEmail is null)
            {
                var skippedPayor = new EmailOutboxItem
                {
                    Recipient = string.Empty,
                    Subject = $"Collection receipt #{collection.Id}",
                    HtmlBody = ComposeReceiptHtml(collection, client, purpose, amountOption),
                    RelatedEntityType = "CollectionTransaction",
                    RelatedEntityId = collection.Id.ToString(),
                    IdempotencyKey = $"collection-receipt:{collection.Id}:PAYOR-MISSING",
                    Status = EmailOutboxStatus.SkippedInvalidRecipient,
                    FailureReason = "No optional payor email was supplied.",
                    AvailableAtUtc = _clock.UtcNow,
                    CreatedAtUtc = _clock.UtcNow
                };
                _dbContext.EmailOutboxItems.Add(skippedPayor);
                _dbContext.EmailLogs.Add(new EmailLog
                {
                    OutboxItemId = skippedPayor.Id,
                    Recipient = string.Empty,
                    Subject = skippedPayor.Subject,
                    HtmlBody = skippedPayor.HtmlBody,
                    Provider = "NotSent",
                    RelatedEntityType = skippedPayor.RelatedEntityType,
                    RelatedEntityId = skippedPayor.RelatedEntityId,
                    AttemptNumber = 0,
                    Status = EmailOutboxStatus.SkippedInvalidRecipient,
                    FailureReason = skippedPayor.FailureReason,
                    CreatedAtUtc = _clock.UtcNow
                });
            }
            var recipients = new[] { collection.PayorEmail, _notificationOptions.SystemCopyAddress }
                .Concat(await _dbContext.CollectionClientUsers.Where(a => a.CollectionClientId == client.Id && a.User.IsActive).Select(a => a.User.Email).ToListAsync(cancellationToken))
                .Where(email => !string.IsNullOrWhiteSpace(email)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var recipient in recipients)
            {
                var valid = IsValidEmail(recipient!);
                _dbContext.EmailOutboxItems.Add(new EmailOutboxItem
                {
                    Recipient = recipient!,
                    Subject = $"Collection receipt #{collection.Id}",
                    HtmlBody = ComposeReceiptHtml(collection, client, purpose, amountOption),
                    RelatedEntityType = "CollectionTransaction",
                    RelatedEntityId = collection.Id.ToString(),
                    IdempotencyKey = $"collection-receipt:{collection.Id}:{recipient!.ToUpperInvariant()}",
                    Status = valid ? EmailOutboxStatus.Pending : EmailOutboxStatus.SkippedInvalidRecipient,
                    FailureReason = valid ? null : "Invalid recipient address.",
                    AvailableAtUtc = _clock.UtcNow,
                    CreatedAtUtc = _clock.UtcNow
                });
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync("COLLECTION_RECORDED", $"CollectionTransaction:{collection.Id}", afterState: new { collection.Id, collection.CollectionClientId, collection.Amount, collection.ProcessingFee, collection.Status }, actorUserId: teller.Id.ToString(), cancellationToken: cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Collection {CollectionId} recorded for client {CollectionClientId}", collection.Id, client.Id);
            return Result.Success(collection);
        }
        catch (Exception exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(exception, "Collection recording failed for client {CollectionClientId}", command.CollectionClientId);
            return Result.Failure<CollectionTransaction>("The collection could not be recorded.");
        }
    }

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    private static string ComposeReceiptHtml(CollectionTransaction collection, CollectionClient client, CollectionPurposeOption purpose, CollectionAmountOption amount) =>
        $"<article><h1>Collection receipt</h1><p>Receipt #{collection.Id}</p><dl><dt>Client</dt><dd>{HtmlEncoder.Default.Encode(client.Name)}</dd><dt>Purpose</dt><dd>{HtmlEncoder.Default.Encode(purpose.Name)}</dd><dt>Amount</dt><dd>{amount.Amount:N2} JMD</dd><dt>Payment date (UTC)</dt><dd>{collection.PaymentDateUtc:yyyy-MM-dd HH:mm}</dd><dt>Method</dt><dd>{collection.Method}</dd></dl></article>";
}
