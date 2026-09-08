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
        var client = new CollectionClient { Name = "Acme", PerJobProcessingFee = 10m }; var otherClient = new CollectionClient { Name = "Other" };
        db.AddRange(manager, client, otherClient); await db.SaveChangesAsync();
        var job = new JobPayment { CollectionClientId = client.Id };
        var valid = new CollectionTransaction { CollectionClientId = client.Id, TellerUserId = manager.Id, PurposeOptionId = Guid.NewGuid(), AmountOptionId = Guid.NewGuid(), PayorName = "Payor", Amount = 1000m, ProcessingFee = 25m, PaymentDateUtc = DateTime.UtcNow };
        var invalid = new CollectionTransaction { CollectionClientId = otherClient.Id, TellerUserId = manager.Id, PurposeOptionId = Guid.NewGuid(), AmountOptionId = Guid.NewGuid(), PayorName = "Payor", Amount = 100m, PaymentDateUtc = DateTime.UtcNow };
        db.AddRange(job, valid, invalid); await db.SaveChangesAsync();
        var service = Service(db);

        var rejected = await service.AttachCollectionAsync(new(manager.Id, job.Id, invalid.Id));
        var attached = await service.AttachCollectionAsync(new(manager.Id, job.Id, valid.Id));

        Assert.True(rejected.IsFailure); Assert.True(attached.IsSuccess);
        Assert.Equal(CollectionStatus.Processing, valid.Status);
        Assert.Equal(1000m, job.JobTotal); Assert.Equal(10m, job.ClientProcessingFee); Assert.Equal(25m, job.TotalTxnProcessingFee); Assert.Equal(965m, job.TotalPaid);
    }

    [Fact]
    public async Task AttachCollectionsAsync_AttachesAllSelectedCollectionsAndRejectsMixedSelectionAtomically()
    {
        await using var db = CreateDb();
        var manager = User(UserRole.Manager, "manager@anonymized.example.com");
        var client = new CollectionClient { Name = "Acme", PerJobProcessingFee = 10m };
        var otherClient = new CollectionClient { Name = "Other" };
        db.AddRange(manager, client, otherClient);
        await db.SaveChangesAsync();
        var job = new JobPayment { CollectionClientId = client.Id };
        var first = Collection(client.Id, manager.Id, 100m, 5m);
        var second = Collection(client.Id, manager.Id, 200m, 7m);
        var invalid = Collection(otherClient.Id, manager.Id, 300m, 0m);
        db.AddRange(job, first, second, invalid);
        await db.SaveChangesAsync();
        var service = Service(db);

        var rejected = await service.AttachCollectionsAsync(new(manager.Id, job.Id, new[] { first.Id, invalid.Id }));
        Assert.True(rejected.IsFailure);
        Assert.Equal(CollectionStatus.Collected, first.Status);
        Assert.Equal(CollectionStatus.Collected, invalid.Status);

        var attached = await service.AttachCollectionsAsync(new(manager.Id, job.Id, new[] { first.Id, second.Id }));
        Assert.True(attached.IsSuccess);
        Assert.Equal(CollectionStatus.Processing, first.Status);
        Assert.Equal(CollectionStatus.Processing, second.Status);
        Assert.Equal(CollectionStatus.Collected, invalid.Status);
        Assert.Equal(2, await db.JobPaymentCollections.CountAsync());
        Assert.Equal(300m, job.JobTotal);
        Assert.Equal(10m, job.ClientProcessingFee);
        Assert.Equal(12m, job.TotalTxnProcessingFee);
        Assert.Equal(278m, job.TotalPaid);
        Assert.Contains(db.AuditRecords, x => x.Action == "JOB_PAYMENT_COLLECTIONS_ATTACHED");
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
    public async Task AttachClaimsAsync_AttachesAllSelectedClaimsAndRejectsMixedSelectionAtomically()
    {
        await using var db = CreateDb();
        var manager = User(UserRole.Manager, "manager@anonymized.example.com");
        var payee = User(UserRole.User, "payee@anonymized.example.com");
        var otherPayee = User(UserRole.User, "other@anonymized.example.com");
        var job = new JobPayment { PayeeUserId = payee.Id };
        var first = new Claim { ClaimantUserId = payee.Id, Title = "Taxi", Description = "Travel", Amount = 100m, Status = ClaimStatus.Accepted };
        var second = new Claim { ClaimantUserId = payee.Id, Title = "Meals", Description = "Travel", Amount = 200m, Status = ClaimStatus.Accepted };
        var invalid = new Claim { ClaimantUserId = otherPayee.Id, Title = "Other", Description = "Travel", Amount = 300m, Status = ClaimStatus.Accepted };
        db.AddRange(manager, payee, otherPayee, job, first, second, invalid);
        await db.SaveChangesAsync();
        var service = Service(db);

        var rejected = await service.AttachClaimsAsync(new(manager.Id, job.Id, new[] { first.Id, invalid.Id }));
        Assert.True(rejected.IsFailure);
        Assert.Equal(ClaimPaymentStatus.Unpaid, first.PaymentStatus);
        Assert.Equal(ClaimPaymentStatus.Unpaid, invalid.PaymentStatus);

        var attached = await service.AttachClaimsAsync(new(manager.Id, job.Id, new[] { first.Id, second.Id }));
        Assert.True(attached.IsSuccess);
        Assert.Equal(ClaimPaymentStatus.Processing, first.PaymentStatus);
        Assert.Equal(ClaimPaymentStatus.Processing, second.PaymentStatus);
        Assert.Equal(ClaimPaymentStatus.Unpaid, invalid.PaymentStatus);
        Assert.Equal(2, await db.JobPaymentClaims.CountAsync());
        Assert.Equal(300m, job.JobTotal);
        Assert.Equal(300m, job.TotalPaid);
        Assert.Contains(db.AuditRecords, x => x.Action == "JOB_PAYMENT_CLAIMS_ATTACHED");
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

    [Fact]
    public async Task MarkPaidAsync_CascadesLinkedStatesAndCannotReplay()
    {
        await using var db = CreateDb();
        var accountant = User(UserRole.Accountant, "accountant@anonymized.example.com"); var payee = User(UserRole.User, "payee@anonymized.example.com");
        var claim = new Claim { ClaimantUserId = payee.Id, Title = "Claim", Description = "Description", Amount = 100m, Status = ClaimStatus.Accepted, PaymentStatus = ClaimPaymentStatus.Processing };
        var client = new CollectionClient { Name = "Client" };
        db.AddRange(accountant, payee, claim, client); await db.SaveChangesAsync();
        var collection = new CollectionTransaction { CollectionClientId = client.Id, TellerUserId = accountant.Id, PurposeOptionId = Guid.NewGuid(), AmountOptionId = Guid.NewGuid(), PayorName = "Payor", Amount = 200m, Status = CollectionStatus.Processing, PaymentDateUtc = DateTime.UtcNow };
        var payroll = new Payroll { UserId = payee.Id, PayrollTotal = 300m, Status = PayrollStatus.Submitted };
        var job = new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Scheduled, JobTotal = 600m, TotalPaid = 600m };
        db.AddRange(collection, payroll, job); await db.SaveChangesAsync();
        db.AddRange(new JobPaymentClaim { JobPaymentId = job.Id, ClaimId = claim.Id }, new JobPaymentCollection { JobPaymentId = job.Id, CollectionTransactionId = collection.Id }, new JobPaymentPayroll { JobPaymentId = job.Id, PayrollId = payroll.Id }); await db.SaveChangesAsync();
        var service = Service(db);

        Assert.True((await service.MarkPaidAsync(job.Id, accountant.Id, DateTime.UtcNow, "TXN-1")).IsSuccess);
        Assert.Equal(JobPaymentStatus.Paid, job.Status); Assert.Equal(ClaimPaymentStatus.Paid, claim.PaymentStatus); Assert.Equal(CollectionStatus.Transferred, collection.Status); Assert.Equal(PayrollStatus.Paid, payroll.Status);
        Assert.Single(db.EmailOutboxItems); Assert.True((await service.MarkPaidAsync(job.Id, accountant.Id, DateTime.UtcNow, "TXN-2")).IsFailure);
    }

    [Fact]
    public async Task AdjustmentWorkflow_PreservesOriginalAndRequiresAdministratorApproval()
    {
        await using var db = CreateDb();
        var accountant = User(UserRole.Accountant, "accountant@anonymized.example.com"); var admin = User(UserRole.Administrator, "admin@anonymized.example.com"); var payee = User(UserRole.User, "payee@anonymized.example.com");
        var original = new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Paid, JobTotal = 500m, TotalPaid = 500m };
        db.AddRange(accountant, admin, payee, original); await db.SaveChangesAsync(); var service = Service(db);

        var created = await service.CreateAdjustmentAsync(new(accountant.Id, original.Id, -75m, "Overpayment recovery"));
        Assert.True(created.IsSuccess); var adjustment = created.Value!;
        Assert.True(adjustment.IsRecoveryReceivable); Assert.Equal(JobPaymentStatus.Paid, original.Status);
        Assert.True((await service.ScheduleAsync(adjustment.Id, accountant.Id, DateTime.UtcNow)).IsFailure);
        Assert.True((await service.ApproveAdjustmentAsync(adjustment.Id, admin.Id)).IsSuccess);
        Assert.True((await service.ScheduleAsync(adjustment.Id, accountant.Id, DateTime.UtcNow)).IsSuccess);
        Assert.Contains(db.AuditRecords, x => x.Action == "JOB_PAYMENT_ADJUSTMENT_APPROVED");
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User User(UserRole role, string email) => new() { Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "User", Role = role };
    private static CollectionTransaction Collection(Guid clientId, Guid tellerId, decimal amount, decimal fee) => new() { CollectionClientId = clientId, TellerUserId = tellerId, PurposeOptionId = Guid.NewGuid(), AmountOptionId = Guid.NewGuid(), PayorName = "Payor", Amount = amount, ProcessingFee = fee, PaymentDateUtc = DateTime.UtcNow };
    private static JobPaymentService Service(ApplicationDbContext db) => new(db, new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<JobPaymentService>.Instance);
}
