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
        var client = new CollectionClient { Name = "Acme", PerTransactionFee = 12.50m };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();
        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Service" };
        var amount = new CollectionAmountOption { CollectionClientId = client.Id, Name = "Standard", Amount = 500m };
        db.AddRange(purpose, amount);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(teller.Id, client.Id, purpose.Id, amount.Id, "Payor", "payor@anonymized.example.com", CollectionMethod.Cash, 20m, DateTime.UtcNow));

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value!.Amount);
        Assert.Equal(12.50m, result.Value.ProcessingFee);
        Assert.Equal(CollectionStatus.Collected, result.Value.Status);
        var receipt = Assert.Single(db.EmailOutboxItems);
        Assert.Equal("payor@anonymized.example.com", receipt.To);
        Assert.Equal("no-reply@anonymized.example.com", receipt.From);
        Assert.Equal("system@anonymized.example.com", receipt.Bcc);
        Assert.Contains(db.AuditRecords, audit => audit.Action == "COLLECTION_RECORDED");
    }

    [Fact]
    public async Task RecordAsync_SnapshotsConfiguredTransactionFeeInsteadOfCallerValue()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme", PerTransactionFee = 18.75m };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(
            teller.Id, client.Id, null, null, "Payor", null, CollectionMethod.Cash,
            ProcessingFee: 999m, PaymentDateUtc: DateTime.UtcNow, Purpose: "Custom", Amount: 100m));

        Assert.True(result.IsSuccess);
        Assert.Equal(18.75m, result.Value!.ProcessingFee);

        client.PerTransactionFee = 50m;
        await db.SaveChangesAsync();
        Assert.Equal(18.75m, result.Value.ProcessingFee);
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
    public async Task RecordAsync_AllowsCustomPurposeAndAmountWithoutCreatingConfiguration()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).RecordAsync(new(teller.Id, client.Id, null, null, "Payor", null, CollectionMethod.Cash, 0m, DateTime.UtcNow, Purpose: "One-time service", Amount: 123.45m));

        Assert.True(result.IsSuccess);
        Assert.Equal("One-time service", result.Value!.Purpose);
        Assert.Equal(123.45m, result.Value.Amount);
        Assert.Null(result.Value.PurposeOptionId);
        Assert.Null(result.Value.AmountOptionId);
        Assert.Empty(db.CollectionPurposeOptions);
        Assert.Empty(db.CollectionAmountOptions);
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
        var skippedPayor = Assert.Single(db.EmailOutboxItems.Where(item => item.Status == EmailOutboxStatus.SkippedInvalidRecipient));
        Assert.Equal(string.Empty, skippedPayor.To);
        Assert.Equal("no-reply@anonymized.example.com", skippedPayor.From);
        Assert.Equal("system@anonymized.example.com", skippedPayor.Bcc);
        Assert.Contains(db.EmailOutboxItems, item => item.To == "no-reply@anonymized.example.com" && item.Bcc == "system@anonymized.example.com" && item.Status == EmailOutboxStatus.Pending);
        Assert.Contains(db.EmailLogs, log => log.Status == EmailOutboxStatus.SkippedInvalidRecipient && log.To == string.Empty && log.From == "no-reply@anonymized.example.com" && log.Bcc == "system@anonymized.example.com");
    }

    [Fact]
    public async Task ReissueReceiptAsync_PersistsHeadersAndDispatchesOnlyVisibleRecipients()
    {
        await using var db = CreateDb();
        var teller = User(UserRole.Teller, "teller@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(teller, client);
        await db.SaveChangesAsync();
        var collection = new CollectionTransaction
        {
            CollectionClientId = client.Id, TellerUserId = teller.Id, PayorName = "Payor", PayorEmail = "payor@anonymized.example.com",
            Purpose = "Service", Amount = 500m, PaymentDateUtc = DateTime.UtcNow
        };
        db.CollectionTransactions.Add(collection);
        db.EmailOutboxItems.Add(new EmailOutboxItem
        {
            To = collection.PayorEmail, From = "no-reply@anonymized.example.com", Bcc = "system@anonymized.example.com",
            Subject = "Original", HtmlBody = "<p>Original</p>", RelatedEntityType = "CollectionTransaction", RelatedEntityId = collection.Id.ToString(),
            IdempotencyKey = "original", Status = EmailOutboxStatus.Sent
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ReissueReceiptAsync(collection.Id, teller.Id);

        Assert.True(result.IsSuccess);
        var reissue = Assert.Single(db.EmailOutboxItems.Where(item => item.IdempotencyKey.StartsWith($"collection-receipt-reissue:{collection.Id}:")));
        Assert.Equal("payor@anonymized.example.com", reissue.To);
        Assert.Equal("no-reply@anonymized.example.com", reissue.From);
        Assert.Equal("system@anonymized.example.com", reissue.Bcc);
        Assert.Equal(EmailOutboxStatus.Pending, reissue.Status);
        Assert.DoesNotContain("system@anonymized.example.com", reissue.HtmlBody, StringComparison.OrdinalIgnoreCase);
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
