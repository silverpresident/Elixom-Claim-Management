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
        if (string.IsNullOrWhiteSpace(command.PayorName) || command.PaymentDateUtc.Kind != DateTimeKind.Utc)
            return Result.Failure<CollectionTransaction>("Payor name and a UTC payment date are required.");

        var teller = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == command.TellerUserId && u.IsActive, cancellationToken);
        if (teller is null || !teller.Role.HasMinimumRole(UserRole.Teller)) return Result.Failure<CollectionTransaction>("Teller access is required.");
        var client = await _dbContext.CollectionClients.SingleOrDefaultAsync(c => c.Id == command.CollectionClientId && c.IsActive, cancellationToken);
        if (client is null) return Result.Failure<CollectionTransaction>("Choose an active client.");

        var purpose = command.PurposeOptionId is { } purposeOptionId
            ? await _dbContext.CollectionPurposeOptions.SingleOrDefaultAsync(o => o.Id == purposeOptionId && o.CollectionClientId == client.Id && o.IsActive, cancellationToken)
            : null;
        if (command.PurposeOptionId.HasValue && purpose is null)
            return Result.Failure<CollectionTransaction>("Choose a purpose suggestion belonging to the selected client.");

        var purposeText = string.IsNullOrWhiteSpace(command.Purpose) ? purpose?.Name : command.Purpose.Trim();
        if (string.IsNullOrWhiteSpace(purposeText) || purposeText.Length > 200)
            return Result.Failure<CollectionTransaction>("A purpose of 200 characters or fewer is required.");
        purpose ??= await _dbContext.CollectionPurposeOptions.SingleOrDefaultAsync(o => o.CollectionClientId == client.Id && o.IsActive && o.Name == purposeText, cancellationToken);

        var amountOption = command.AmountOptionId is { } amountOptionId
            ? await _dbContext.CollectionAmountOptions.SingleOrDefaultAsync(o => o.Id == amountOptionId && o.CollectionClientId == client.Id && o.IsActive, cancellationToken)
            : null;
        if (command.AmountOptionId.HasValue && amountOption is null)
            return Result.Failure<CollectionTransaction>("Choose an amount suggestion belonging to the selected client.");

        var amount = command.Amount ?? amountOption?.Amount;
        if (amount is null || amount <= 0)
            return Result.Failure<CollectionTransaction>("A positive amount is required.");
        amountOption ??= await _dbContext.CollectionAmountOptions.SingleOrDefaultAsync(o => o.CollectionClientId == client.Id && o.IsActive && o.Amount == amount, cancellationToken);

        await using var transaction = _dbContext.Database.IsRelational() ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var collection = new CollectionTransaction
            {
                CollectionClientId = client.Id,
                PurposeOptionId = purpose?.Id,
                Purpose = purposeText,
                AmountOptionId = amountOption?.Id,
                TellerUserId = teller.Id,
                PayorName = command.PayorName.Trim(),
                PayorEmail = string.IsNullOrWhiteSpace(command.PayorEmail) ? null : command.PayorEmail.Trim(),
                PayorTelephone = string.IsNullOrWhiteSpace(command.PayorTelephone) ? null : command.PayorTelephone.Trim(),
                ReferenceNumber = string.IsNullOrWhiteSpace(command.ReferenceNumber) ? null : command.ReferenceNumber.Trim(),
                Method = command.Method,
                Status = CollectionStatus.Collected,
                Amount = amount.Value,
                // Financial fee authority belongs to the configured client.  Persist the
                // value on the transaction so later client configuration changes cannot
                // rewrite a collected financial record.
                ProcessingFee = client.PerTransactionFee,
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
                    Subject = $"Collection receipt #{collection.SequenceNo}",
                    HtmlBody = ComposeReceiptHtml(collection, client),
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
                    Subject = $"Collection receipt #{collection.SequenceNo}",
                    HtmlBody = ComposeReceiptHtml(collection, client),
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

    public async Task<Result> ReissueReceiptAsync(Guid collectionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var actor = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == actorUserId && u.IsActive, cancellationToken);
        var collection = await _dbContext.CollectionTransactions.Include(c => c.CollectionClient).Include(c => c.PurposeOption).Include(c => c.AmountOption).SingleOrDefaultAsync(c => c.Id == collectionId, cancellationToken);
        if (actor is null || collection is null || !actor.Role.HasMinimumRole(UserRole.Teller)) return Result.Failure("Collection receipt was not found.");
        if (actor.Id != collection.TellerUserId && !actor.Role.HasMinimumRole(UserRole.Manager)) return Result.Failure("Only the recording teller or a manager may reissue this receipt.");

        var recipients = await _dbContext.EmailOutboxItems.Where(e => e.RelatedEntityType == "CollectionTransaction" && e.RelatedEntityId == collection.Id.ToString() && e.Status != EmailOutboxStatus.SkippedInvalidRecipient).Select(e => e.Recipient).Distinct().ToListAsync(cancellationToken);
        if (recipients.Count == 0) return Result.Failure("There are no valid configured receipt recipients.");
        foreach (var recipient in recipients.Where(IsValidEmail))
        {
            _dbContext.EmailOutboxItems.Add(new EmailOutboxItem
            {
                Recipient = recipient,
                Subject = $"Collection receipt reissue #{collection.SequenceNo}",
                HtmlBody = ComposeReceiptHtml(collection, collection.CollectionClient),
                RelatedEntityType = "CollectionTransaction",
                RelatedEntityId = collection.Id.ToString(),
                IdempotencyKey = $"collection-receipt-reissue:{collection.Id}:{Guid.NewGuid():N}",
                Status = EmailOutboxStatus.Pending,
                AvailableAtUtc = _clock.UtcNow,
                CreatedAtUtc = _clock.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync("COLLECTION_RECEIPT_REISSUED", $"CollectionTransaction:{collection.Id}", actorUserId: actor.Id.ToString(), cancellationToken: cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<CollectionReadModel>>> ListForActorAsync(Guid actorUserId, Guid? collectionClientId, int take, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Users.Where(user => user.Id == actorUserId && user.IsActive).Select(user => (UserRole?)user.Role).SingleOrDefaultAsync(cancellationToken);
        if (role is not { } activeRole || !activeRole.HasMinimumRole(UserRole.Teller)) return Result.Failure<IReadOnlyList<CollectionReadModel>>("Teller access is required.");
        var boundedTake = Math.Clamp(take, 1, 100);
        var query = _dbContext.CollectionTransactions.AsNoTracking();
        if (collectionClientId.HasValue) query = query.Where(collection => collection.CollectionClientId == collectionClientId.Value);
        var records = await query.OrderByDescending(collection => collection.CreatedAtUtc).Take(boundedTake)
            .Select(collection => new CollectionReadModel(collection.Id, collection.SequenceNo, collection.CollectionClientId, collection.PayorName, collection.Method, collection.Status, collection.Amount, collection.Currency, collection.PaymentDateUtc, collection.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<CollectionReadModel>>(records);
    }

    public async Task<Result<CollectionReadModel>> GetForActorAsync(Guid actorUserId, Guid collectionId, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Users.Where(user => user.Id == actorUserId && user.IsActive).Select(user => (UserRole?)user.Role).SingleOrDefaultAsync(cancellationToken);
        if (role is not { } activeRole || !activeRole.HasMinimumRole(UserRole.Teller)) return Result.Failure<CollectionReadModel>("Teller access is required.");
        var record = await _dbContext.CollectionTransactions.AsNoTracking().Where(collection => collection.Id == collectionId)
            .Select(collection => new CollectionReadModel(collection.Id, collection.SequenceNo, collection.CollectionClientId, collection.PayorName, collection.Method, collection.Status, collection.Amount, collection.Currency, collection.PaymentDateUtc, collection.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? Result.Failure<CollectionReadModel>("Collection record was not found.") : Result.Success(record);
    }

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    private static string ComposeReceiptHtml(CollectionTransaction collection, CollectionClient client) =>
        $"<article><h1>Collection receipt</h1><p>Receipt #{collection.SequenceNo}</p><dl><dt>Client</dt><dd>{HtmlEncoder.Default.Encode(client.Name)}</dd><dt>Purpose</dt><dd>{HtmlEncoder.Default.Encode(collection.Purpose)}</dd><dt>Amount</dt><dd>{collection.Amount:N2} JMD</dd><dt>Payment date (UTC)</dt><dd>{collection.PaymentDateUtc:yyyy-MM-dd HH:mm}</dd><dt>Method</dt><dd>{collection.Method}</dd></dl></article>";
}
