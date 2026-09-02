using System.Net.Mail;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class OutboxService : IOutboxService
{
    private const int MaximumAttempts = 5;
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailSender _sender;
    private readonly ISystemClock _clock;
    private readonly ILogger<OutboxService> _logger;
    public OutboxService(ApplicationDbContext dbContext, IEmailSender sender, ISystemClock clock, ILogger<OutboxService> logger) { _dbContext = dbContext; _sender = sender; _clock = clock; _logger = logger; }

    public async Task<int> DispatchDueAsync(int batchSize = 25, CancellationToken cancellationToken = default)
    {
        var due = await _dbContext.EmailOutboxItems.Where(e => e.Status == EmailOutboxStatus.Pending && e.AvailableAtUtc <= _clock.UtcNow).OrderBy(e => e.CreatedAtUtc).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var item in due)
        {
            if (!IsValidEmail(item.Recipient)) { await RecordOutcomeAsync(item, EmailOutboxStatus.SkippedInvalidRecipient, "Invalid recipient address.", cancellationToken); continue; }
            item.Status = EmailOutboxStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);
            var result = await _sender.SendAsync(new EmailMessage(item.Recipient, item.Subject, item.HtmlBody), cancellationToken);
            if (result.Succeeded) await RecordOutcomeAsync(item, EmailOutboxStatus.Sent, null, cancellationToken);
            else
            {
                item.AttemptCount++;
                var retry = item.AttemptCount < MaximumAttempts;
                item.Status = retry ? EmailOutboxStatus.Pending : EmailOutboxStatus.Failed;
                item.AvailableAtUtc = _clock.UtcNow.AddMinutes(Math.Pow(2, item.AttemptCount));
                item.FailureReason = result.FailureReason ?? "Email delivery failed.";
                await AddLogAsync(item, item.Status, item.FailureReason, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        return due.Count;
    }

    private async Task RecordOutcomeAsync(EmailOutboxItem item, EmailOutboxStatus status, string? reason, CancellationToken cancellationToken)
    {
        item.Status = status; item.FailureReason = reason; if (status == EmailOutboxStatus.Sent) item.SentAtUtc = _clock.UtcNow;
        await AddLogAsync(item, status, reason, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task AddLogAsync(EmailOutboxItem item, EmailOutboxStatus status, string? reason, CancellationToken cancellationToken)
    {
        _dbContext.EmailLogs.Add(new EmailLog { OutboxItemId = item.Id, Recipient = item.Recipient, Subject = item.Subject, HtmlBody = item.HtmlBody, Provider = _sender.ProviderName, RelatedEntityType = item.RelatedEntityType, RelatedEntityId = item.RelatedEntityId, AttemptNumber = item.AttemptCount + 1, Status = status, FailureReason = reason, CreatedAtUtc = _clock.UtcNow });
        _logger.LogInformation("Outbox email {OutboxId} finished with {Status}", item.Id, status);
        return Task.CompletedTask;
    }

    private static bool IsValidEmail(string value) { try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); } catch (FormatException) { return false; } }
}
