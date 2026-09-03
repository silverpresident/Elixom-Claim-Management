using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class ConcurrencyAndSettlementTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task MarkPaidAsync_ConcurrentSettlementAttempts_OnlyOneSucceeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbInit = CreateDbContext(dbName);

        var accountant = new User { Id = Guid.NewGuid(), Email = "accountant@elixom.com", FullName = "Accountant", Role = UserRole.Accountant, IsActive = true };
        var payee = new User { Id = Guid.NewGuid(), Email = "payee@elixom.com", FullName = "Payee", Role = UserRole.User, IsActive = true };
        dbInit.Users.AddRange(accountant, payee);

        var job = new JobPayment
        {
            PayeeUserId = payee.Id,
            Status = JobPaymentStatus.Scheduled,
            JobTotal = 5000m,
            TotalPaid = 5000m,
            ScheduledAtUtc = DateTime.UtcNow
        };
        dbInit.JobPayments.Add(job);
        await dbInit.SaveChangesAsync();

        var db1 = CreateDbContext(dbName);
        var service1 = new JobPaymentService(db1, new AuditService(db1, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<JobPaymentService>.Instance);

        var db2 = CreateDbContext(dbName);
        var service2 = new JobPaymentService(db2, new AuditService(db2, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<JobPaymentService>.Instance);

        var task1 = service1.MarkPaidAsync(job.Id, accountant.Id, DateTime.UtcNow, "TXN-001");
        var task2 = service2.MarkPaidAsync(job.Id, accountant.Id, DateTime.UtcNow, "TXN-002");

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => r.IsFailure);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var dbCheck = CreateDbContext(dbName);
        var finalJob = await dbCheck.JobPayments.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(JobPaymentStatus.Paid, finalJob.Status);
    }

    [Fact]
    public async Task DispatchDueAsync_ConcurrentOutboxWorkers_ProcessAtomicallyWithoutDuplicates()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbInit = CreateDbContext(dbName);

        for (int i = 1; i <= 5; i++)
        {
            dbInit.EmailOutboxItems.Add(new EmailOutboxItem
            {
                Recipient = $"test{i}@example.com",
                Subject = $"Subject {i}",
                HtmlBody = "<p>Test</p>",
                RelatedEntityType = "Test",
                RelatedEntityId = i.ToString(),
                IdempotencyKey = $"test-item-{i}",
                Status = EmailOutboxStatus.Pending,
                AvailableAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        await dbInit.SaveChangesAsync();

        var db1 = CreateDbContext(dbName);
        var outbox1 = new OutboxService(db1, new FakeEmailSender(), new SystemClock(), NullLogger<OutboxService>.Instance);

        var db2 = CreateDbContext(dbName);
        var outbox2 = new OutboxService(db2, new FakeEmailSender(), new SystemClock(), NullLogger<OutboxService>.Instance);

        var task1 = outbox1.DispatchDueAsync(10);
        var task2 = outbox2.DispatchDueAsync(10);

        var results = await Task.WhenAll(task1, task2);

        var dbCheck = CreateDbContext(dbName);
        var sentCount = await dbCheck.EmailOutboxItems.CountAsync(e => e.Status == EmailOutboxStatus.Sent);
        Assert.Equal(5, sentCount);
    }
}
