using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class JobPaymentServiceTests
{
    [Fact]
    public async Task AttachCollectionAsync_RejectsDifferentClientAndRecalculatesValidJob()
    {
        await using var db = CreateDb();
        var manager = User(UserRole.Manager, "manager@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme" }; var otherClient = new CollectionClient { Name = "Other" };
        db.AddRange(manager, client, otherClient); await db.SaveChangesAsync();
        var job = new JobPayment { CollectionClientId = client.Id };
        var valid = new CollectionTransaction { CollectionClientId = client.Id, TellerUserId = manager.Id, PurposeOptionId = 1, AmountOptionId = 1, PayorName = "Payor", Amount = 1000m, ProcessingFee = 25m, PaymentDateUtc = DateTime.UtcNow };
        var invalid = new CollectionTransaction { CollectionClientId = otherClient.Id, TellerUserId = manager.Id, PurposeOptionId = 1, AmountOptionId = 1, PayorName = "Payor", Amount = 100m, PaymentDateUtc = DateTime.UtcNow };
        db.AddRange(job, valid, invalid); await db.SaveChangesAsync();
        var service = Service(db);

        var rejected = await service.AttachCollectionAsync(new(manager.Id, job.Id, invalid.Id));
        var attached = await service.AttachCollectionAsync(new(manager.Id, job.Id, valid.Id));

        Assert.True(rejected.IsFailure); Assert.True(attached.IsSuccess);
        Assert.Equal(CollectionStatus.Processing, valid.Status);
        Assert.Equal(1000m, job.JobTotal); Assert.Equal(25m, job.ClientProcessingFee); Assert.Equal(975m, job.TotalPaid);
    }

    [Fact]
    public async Task AttachClaimAsync_RequiresAcceptedClaimForPayeeAndRestoresStateOnRemoval()
    {
        await using var db = CreateDb();
        var manager = User(UserRole.Manager, "manager@anonymized.example.com"); var payee = User(UserRole.User, "payee@anonymized.example.com");
        db.AddRange(manager, payee); await db.SaveChangesAsync();
        var job = new JobPayment { PayeeUserId = payee.Id }; var claim = new Claim { ClaimantUserId = payee.Id, Title = "Taxi", Description = "Travel", Amount = 200m, Status = ClaimStatus.Accepted };
        db.AddRange(job, claim); await db.SaveChangesAsync();
        var service = Service(db);

        Assert.True((await service.AttachClaimAsync(new(manager.Id, job.Id, claim.Id))).IsSuccess);
        Assert.Equal(ClaimPaymentStatus.Processing, claim.PaymentStatus); Assert.Equal(200m, job.TotalPaid);
        Assert.True((await service.RemoveClaimAsync(new(manager.Id, job.Id, claim.Id))).IsSuccess);
        Assert.Equal(ClaimPaymentStatus.Unpaid, claim.PaymentStatus); Assert.Equal(0m, job.TotalPaid);
    }

    [Fact]
    public async Task SubmitAndScheduleAsync_RequireLifecycleAndAccountantAuthority()
    {
        await using var db = CreateDb();
        var manager = User(UserRole.Manager, "manager@anonymized.example.com");
        var accountant = User(UserRole.Accountant, "accountant@anonymized.example.com");
        var payee = User(UserRole.User, "payee@anonymized.example.com");
        var job = new JobPayment { PayeeUserId = payee.Id, JobTotal = 100m, TotalPaid = 100m };
        db.AddRange(manager, accountant, payee, job); await db.SaveChangesAsync();
        var service = Service(db);

        Assert.True((await service.SubmitAsync(job.Id, manager.Id)).IsSuccess);
        Assert.Equal(JobPaymentStatus.Submitted, job.Status);
        Assert.True((await service.ScheduleAsync(job.Id, manager.Id, DateTime.UtcNow)).IsFailure);
        Assert.True((await service.ScheduleAsync(job.Id, accountant.Id, DateTime.UtcNow)).IsSuccess);
        Assert.Equal(JobPaymentStatus.Scheduled, job.Status);
        Assert.Contains(db.AuditRecords, x => x.Action == "JOB_PAYMENT_SCHEDULED");
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User User(UserRole role, string email) => new() { Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "User", Role = role };
    private static JobPaymentService Service(ApplicationDbContext db) => new(db, new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<JobPaymentService>.Instance);
}
