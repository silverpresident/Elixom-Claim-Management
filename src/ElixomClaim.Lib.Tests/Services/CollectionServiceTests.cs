using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class CollectionServiceTests
{
    [Fact]
    public async Task RecordAsync_ValidatesClientOptionsAndAtomicallyCreatesReceiptOutboxAndAudit()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();
        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Service" };
        var amount = new CollectionAmountOption { CollectionClientId = client.Id, Name = "Standard", Amount = 500m };
        db.AddRange(purpose, amount);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(teller.Id, client.Id, purpose.Id, amount.Id, "Payor", "payor@anonymized.example.com", CollectionMethod.Cash, 20m, DateTime.UtcNow));

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value!.Amount);
        Assert.Equal(20m, result.Value.ProcessingFee);
        Assert.Equal(CollectionStatus.Collected, result.Value.Status);
        Assert.Equal(2, db.EmailOutboxItems.Count()); // payor and system-copy recipients
        Assert.Contains(db.AuditRecords, audit => audit.Action == "COLLECTION_RECORDED");
    }

    [Fact]
    public async Task RecordAsync_RejectsOptionFromAnotherClient()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        var otherClient = new CollectionClient { Name = "Other" };
        db.AddRange(teller, client, otherClient);
        await db.SaveChangesAsync();
        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Service" };
        var otherAmount = new CollectionAmountOption { CollectionClientId = otherClient.Id, Name = "Standard", Amount = 500m };
        db.AddRange(purpose, otherAmount);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(teller.Id, client.Id, purpose.Id, otherAmount.Id, "Payor", null, CollectionMethod.Cash, 0m, DateTime.UtcNow));

        Assert.True(result.IsFailure);
        Assert.Empty(db.CollectionTransactions);
    }

    [Fact]
    public async Task RecordAsync_MissingOptionalPayorEmail_IsRecordedAsSkippedWithoutBlockingSystemReceipt()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();
        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Service" };
        var amount = new CollectionAmountOption { CollectionClientId = client.Id, Name = "Standard", Amount = 500m };
        db.AddRange(purpose, amount);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(teller.Id, client.Id, purpose.Id, amount.Id, "Payor", null, CollectionMethod.Cash, 0m, DateTime.UtcNow));

        Assert.True(result.IsSuccess);
        Assert.Contains(db.EmailOutboxItems, item => item.Status == EmailOutboxStatus.SkippedInvalidRecipient);
        Assert.Contains(db.EmailOutboxItems, item => item.Recipient == "system@anonymized.example.com" && item.Status == EmailOutboxStatus.Pending);
        Assert.Contains(db.EmailLogs, log => log.Status == EmailOutboxStatus.SkippedInvalidRecipient);
    }

    [Fact]
    public async Task RecordAsync_ReceiptBodyDoesNotExposeInternalProcessingFee()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();
        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Service" };
        var amount = new CollectionAmountOption { CollectionClientId = client.Id, Name = "Standard", Amount = 500m };
        db.AddRange(purpose, amount);
        await db.SaveChangesAsync();

        await CreateService(db).RecordAsync(new(teller.Id, client.Id, purpose.Id, amount.Id, "Payor", "payor@anonymized.example.com", CollectionMethod.Cash, 44.44m, DateTime.UtcNow));

        Assert.All(db.EmailOutboxItems, item => Assert.DoesNotContain("Processing fee", item.HtmlBody, StringComparison.OrdinalIgnoreCase));
        Assert.All(db.EmailOutboxItems, item => Assert.DoesNotContain("44.44", item.HtmlBody, StringComparison.Ordinal));
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User User(UserRole role, string email) => new() { Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "Anonymized user", Role = role };
    private static CollectionService CreateService(ApplicationDbContext db) => new(db, new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), Options.Create(new NotificationOptions { Provider = "Disabled", FromAddress = "no-reply@anonymized.example.com", SystemCopyAddress = "system@anonymized.example.com" }), NullLogger<CollectionService>.Instance);
}
