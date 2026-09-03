using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class McpToolBoundaryTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EmailTools_Preview_RedactsSensitiveDataAndRestrictsFreeFormTemplates()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var clock = new SystemClock();
        var notificationOpts = Options.Create(new NotificationOptions { SystemCopyAddress = "ops@elixom.com" });
        var emailTools = new EmailTools(db, audit, clock, notificationOpts);

        var teller = new User { Id = Guid.NewGuid(), Email = "teller@elixom.com", FullName = "Teller", Role = UserRole.Teller, IsActive = true };
        db.Users.Add(teller);

        // Prohibited arbitrary template request
        var freeFormReq = new EmailPreviewRequest("FreeFormCustomTemplate", 1);
        var freeFormRes = await emailTools.PreviewAsync(teller, freeFormReq, CancellationToken.None);
        Assert.False(freeFormRes.Success);
        Assert.Contains("Prohibited", freeFormRes.Error, StringComparison.OrdinalIgnoreCase);

        // Valid CollectionReceipt template
        var client = new CollectionClient { Name = "Test Client", IsActive = true };
        db.CollectionClients.Add(client);
        await db.SaveChangesAsync();

        var purpose = new CollectionPurposeOption { CollectionClientId = client.Id, Name = "Fee Payment", IsActive = true };
        var amount = new CollectionAmountOption { CollectionClientId = client.Id, Name = "Full", Amount = 500m, IsActive = true };
        db.CollectionPurposeOptions.Add(purpose);
        db.CollectionAmountOptions.Add(amount);
        await db.SaveChangesAsync();

        var collection = new CollectionTransaction
        {
            CollectionClientId = client.Id,
            PurposeOptionId = purpose.Id,
            AmountOptionId = amount.Id,
            TellerUserId = teller.Id,
            PayorName = "John Doe",
            PayorEmail = "john.doe@example.com",
            Method = CollectionMethod.Cash,
            Amount = 500m,
            ProcessingFee = 25m, // Sensitive internal processing fee
            PaymentDateUtc = DateTime.UtcNow
        };
        db.CollectionTransactions.Add(collection);
        await db.SaveChangesAsync();

        var previewRes = await emailTools.PreviewAsync(teller, new EmailPreviewRequest("CollectionReceipt", collection.Id), CancellationToken.None);
        Assert.True(previewRes.Success);
        Assert.NotNull(previewRes.RedactedHtmlBody);
        Assert.DoesNotContain("25.00", previewRes.RedactedHtmlBody); // Processing fee hidden in receipt
        Assert.NotNull(previewRes.RecipientSummary);
        Assert.Contains("j******e@example.com", previewRes.RecipientSummary); // Email redacted in summary preview
    }

    [Fact]
    public async Task EmailTools_QueueSend_UsesDurableOutboxWithIdempotencyDeduplication()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var clock = new SystemClock();
        var notificationOpts = Options.Create(new NotificationOptions { SystemCopyAddress = "ops@elixom.com" });
        var emailTools = new EmailTools(db, audit, clock, notificationOpts);

        var accountant = new User { Id = Guid.NewGuid(), Email = "accountant@elixom.com", FullName = "Accountant", Role = UserRole.Accountant, IsActive = true };
        var payee = new User { Id = Guid.NewGuid(), Email = "payee@elixom.com", FullName = "Payee", Role = UserRole.User, IsActive = true, BankAccountNumber = "12345678" };
        db.Users.AddRange(accountant, payee);

        var job = new JobPayment
        {
            PayeeUserId = payee.Id,
            Status = JobPaymentStatus.Paid,
            JobTotal = 1000m,
            TotalPaid = 1000m,
            PaymentTransactionNumber = "TXN-99887766",
            PaymentDateUtc = DateTime.UtcNow
        };
        db.JobPayments.Add(job);
        await db.SaveChangesAsync();

        // Send 1: Queues outbox items
        var queueReq = new EmailQueueSendRequest("PaymentSummary", job.Id, "KEY-1001");
        var queueRes = await emailTools.QueueSendAsync(accountant, queueReq, CancellationToken.None);
        Assert.True(queueRes.Success);
        Assert.Equal(1, queueRes.QueuedCount);

        var outboxItems = await db.EmailOutboxItems.ToListAsync();
        Assert.Single(outboxItems);
        Assert.Equal("payee@elixom.com", outboxItems[0].Recipient);
        Assert.Equal(EmailOutboxStatus.Pending, outboxItems[0].Status);

        // Send 2 with same idempotency key: Deduplicated, no new items added
        var retryRes = await emailTools.QueueSendAsync(accountant, queueReq, CancellationToken.None);
        Assert.True(retryRes.Success);
        Assert.Equal(0, retryRes.QueuedCount);
        Assert.Single(await db.EmailOutboxItems.ToListAsync());
    }

    [Fact]
    public async Task OperationsTools_OutboxWakeUp_RequiresAdmin_And_Deduplicates()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var outbox = new OutboxService(db, new FakeEmailSender(), new SystemClock(), NullLogger<OutboxService>.Instance);
        var salary = new SalaryPayrollService(db, new SalaryRecurrencePlanner(), audit, new SystemClock(), NullLogger<SalaryPayrollService>.Instance);
        var opsTools = new OperationsTools(salary, outbox, audit, new SystemClock());

        var user = new User { Id = Guid.NewGuid(), Email = "user@elixom.com", FullName = "User", Role = UserRole.User, IsActive = true };
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@elixom.com", FullName = "Admin", Role = UserRole.Administrator, IsActive = true };
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();

        // Non-admin call fails
        var userRes = await opsTools.RequestOutboxWakeUpAsync(user, new OutboxWakeUpRequest(25, "IDEM-1"), CancellationToken.None);
        Assert.False(userRes.Success);
        Assert.Contains("Administrator role required", userRes.Error, StringComparison.OrdinalIgnoreCase);

        // Admin call succeeds
        var adminRes = await opsTools.RequestOutboxWakeUpAsync(admin, new OutboxWakeUpRequest(25, "IDEM-1"), CancellationToken.None);
        Assert.True(adminRes.Success);
        Assert.NotNull(adminRes.Record);
        Assert.Equal("Completed", adminRes.Record.Status);

        // Status polling
        var statusRes = await opsTools.GetOperationStatusAsync(admin, new OperationStatusRequest("IDEM-1"), CancellationToken.None);
        Assert.True(statusRes.Success);
        Assert.Equal("Completed", statusRes.Record?.Status);
    }
}
