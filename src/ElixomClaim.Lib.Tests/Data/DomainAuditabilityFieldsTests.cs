using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class DomainAuditabilityFieldsTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CollectionPurposeOption_And_CollectionAmountOption_HaveCreatedAtUtc()
    {
        var db = CreateInMemoryDbContext();
        var client = new CollectionClient { Id = Guid.NewGuid(), Name = "Test Client" };
        db.CollectionClients.Add(client);

        var purposeOption = new CollectionPurposeOption
        {
            Id = Guid.NewGuid(),
            CollectionClientId = client.Id,
            Name = "Registration Fee"
        };

        var amountOption = new CollectionAmountOption
        {
            Id = Guid.NewGuid(),
            CollectionClientId = client.Id,
            Name = "Standard Rate",
            Amount = 5000.00m
        };

        db.CollectionPurposeOptions.Add(purposeOption);
        db.CollectionAmountOptions.Add(amountOption);
        await db.SaveChangesAsync();

        var fetchedPurpose = await db.CollectionPurposeOptions.FindAsync(purposeOption.Id);
        var fetchedAmount = await db.CollectionAmountOptions.FindAsync(amountOption.Id);

        Assert.NotNull(fetchedPurpose);
        Assert.NotEqual(default, fetchedPurpose.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, fetchedPurpose.CreatedAtUtc.Kind);

        Assert.NotNull(fetchedAmount);
        Assert.NotEqual(default, fetchedAmount.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, fetchedAmount.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task SalaryAdjustment_HasCreatedAtUtc()
    {
        var db = CreateInMemoryDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "worker@elixom.com", FullName = "Worker" };
        var salaryDef = new SalaryDefinition
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Description = "Base Salary",
            BaseAmount = 100000m,
            FirstSalaryDate = new DateOnly(2026, 1, 1),
            LastSalaryDate = new DateOnly(2026, 1, 1),
            StartDate = new DateOnly(2026, 1, 1),
            RecurrenceMonths = 1
        };
        db.Users.Add(user);
        db.SalaryDefinitions.Add(salaryDef);

        var adj = new SalaryAdjustment
        {
            Id = Guid.NewGuid(),
            SalaryDefinitionId = salaryDef.Id,
            Title = "Health Benefit",
            PercentageRate = 0.05m,
            Type = SalaryAdjustmentType.Benefit
        };

        db.SalaryAdjustments.Add(adj);
        await db.SaveChangesAsync();

        var fetchedAdj = await db.SalaryAdjustments.FindAsync(adj.Id);
        Assert.NotNull(fetchedAdj);
        Assert.NotEqual(default, fetchedAdj.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, fetchedAdj.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task Claim_DateOfJob_DefaultsAndCustomValuesArePersisted()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var userId = Guid.NewGuid();
        var jobDate = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

        var defaultClaim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Default Claim", "Desc", 1000m));
        var customClaim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Custom Claim", "Desc", 2000m, DateOfJob: jobDate));

        Assert.NotEqual(default, defaultClaim.DateOfJob);
        Assert.Equal(DateTimeKind.Utc, defaultClaim.DateOfJob.Kind);

        Assert.Equal(jobDate, customClaim.DateOfJob);
        Assert.Equal(DateTimeKind.Utc, customClaim.DateOfJob.Kind);
    }

    [Fact]
    public async Task Claim_SoftDelete_SetsDeletedAtUtc()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);

        var userId = Guid.NewGuid();
        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Claim To Delete", "Desc", 1500m));

        Assert.Null(claim.DeletedAtUtc);

        var result = await claimService.SoftDeleteAsync(new SoftDeleteClaimCommand(claim.Id, userId));
        Assert.True(result);

        // Fetch without query filter to inspect soft-deleted entity
        var deletedClaim = await db.Claims.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == claim.Id);
        Assert.NotNull(deletedClaim);
        Assert.True(deletedClaim.IsDeleted);
        Assert.NotNull(deletedClaim.DeletedAtUtc);
        Assert.Equal(DateTimeKind.Utc, deletedClaim.DeletedAtUtc.Value.Kind);
    }

    [Fact]
    public async Task OutboxService_DispatchDueAsync_PopulatesEmailLogSentAtUtc_WhenSent()
    {
        var db = CreateInMemoryDbContext();
        var clock = new SystemClock();
        var sender = new FakeEmailSender();
        var outboxService = new OutboxService(db, sender, clock, NullLogger<OutboxService>.Instance);

        var outboxItem = new EmailOutboxItem
        {
            Id = Guid.NewGuid(),
            To = "payee@example.com",
            Subject = "Test Email",
            HtmlBody = "<p>Hello</p>",
            RelatedEntityType = "JobPayment",
            RelatedEntityId = Guid.NewGuid().ToString(),
            IdempotencyKey = "test-key-1",
            Status = EmailOutboxStatus.Pending,
            AvailableAtUtc = clock.UtcNow
        };
        db.EmailOutboxItems.Add(outboxItem);
        await db.SaveChangesAsync();

        var dispatchedCount = await outboxService.DispatchDueAsync(10);
        Assert.Equal(1, dispatchedCount);

        var log = await db.EmailLogs.FirstOrDefaultAsync(l => l.OutboxItemId == outboxItem.Id);
        Assert.NotNull(log);
        Assert.Equal(EmailOutboxStatus.Sent, log.Status);
        Assert.NotNull(log.SentAtUtc);
        Assert.Equal(DateTimeKind.Utc, log.SentAtUtc.Value.Kind);
    }

    private class FakeEmailSender : IEmailSender
    {
        public string ProviderName => "FakeSender";

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResult(true));
        }
    }
}
