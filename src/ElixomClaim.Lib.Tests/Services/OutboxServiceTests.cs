using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class OutboxServiceTests
{
    [Fact]
    public async Task DispatchDueAsync_SendsOnceAndWritesAnEmailLog()
    {
        await using var db = CreateDb();
        db.EmailOutboxItems.Add(Outbox("payor@anonymized.example.com"));
        await db.SaveChangesAsync();
        var sender = new StubEmailSender(success: true);
        var service = new OutboxService(db, sender, new SystemClock(), NullLogger<OutboxService>.Instance);

        await service.DispatchDueAsync();
        await service.DispatchDueAsync();

        var item = await db.EmailOutboxItems.SingleAsync();
        Assert.Equal(EmailOutboxStatus.Sent, item.Status);
        Assert.Single(sender.Messages);
        Assert.Single(db.EmailLogs);
        Assert.Equal(EmailOutboxStatus.Sent, db.EmailLogs.Single().Status);
    }

    [Fact]
    public async Task DispatchDueAsync_InvalidRecipientIsRecordedWithoutSending()
    {
        await using var db = CreateDb();
        db.EmailOutboxItems.Add(Outbox("not-an-email"));
        await db.SaveChangesAsync();
        var sender = new StubEmailSender(success: true);

        await new OutboxService(db, sender, new SystemClock(), NullLogger<OutboxService>.Instance).DispatchDueAsync();

        Assert.Equal(EmailOutboxStatus.SkippedInvalidRecipient, db.EmailOutboxItems.Single().Status);
        Assert.Empty(sender.Messages);
        Assert.Equal(EmailOutboxStatus.SkippedInvalidRecipient, db.EmailLogs.Single().Status);
    }

    [Fact]
    public async Task DispatchDueAsync_FailureSchedulesExponentialRetry()
    {
        await using var db = CreateDb();
        db.EmailOutboxItems.Add(Outbox("payor@anonymized.example.com"));
        await db.SaveChangesAsync();
        var before = DateTime.UtcNow;

        await new OutboxService(db, new StubEmailSender(success: false), new SystemClock(), NullLogger<OutboxService>.Instance).DispatchDueAsync();

        var item = db.EmailOutboxItems.Single();
        Assert.Equal(EmailOutboxStatus.Pending, item.Status);
        Assert.Equal(1, item.AttemptCount);
        Assert.True(item.AvailableAtUtc >= before.AddMinutes(2));
        Assert.Single(db.EmailLogs);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static EmailOutboxItem Outbox(string recipient) => new() { Recipient = recipient, Subject = "Receipt", HtmlBody = "<p>Receipt</p>", RelatedEntityType = "CollectionTransaction", RelatedEntityId = "1", IdempotencyKey = Guid.NewGuid().ToString("N"), AvailableAtUtc = DateTime.UtcNow.AddMinutes(-1) };

    private sealed class StubEmailSender(bool success) : IEmailSender
    {
        public string ProviderName => "Fake";
        public List<EmailMessage> Messages { get; } = [];
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.FromResult(new EmailSendResult(success, success ? null : "Fake failure")); }
    }
}
