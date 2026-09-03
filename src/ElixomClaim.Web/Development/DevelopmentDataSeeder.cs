using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Development;

/// <summary>Seeds non-sensitive, deterministic sample data for local Development use only.</summary>
public static class DevelopmentDataSeeder
{
    public static readonly IReadOnlyDictionary<UserRole, Guid> UserIds = new Dictionary<UserRole, Guid>
    {
        [UserRole.User] = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        [UserRole.Teller] = Guid.Parse("10000000-0000-0000-0000-000000000002"),
        [UserRole.Manager] = Guid.Parse("10000000-0000-0000-0000-000000000003"),
        [UserRole.Accountant] = Guid.Parse("10000000-0000-0000-0000-000000000004"),
        [UserRole.Administrator] = Guid.Parse("10000000-0000-0000-0000-000000000005"),
        [UserRole.Blocked] = Guid.Parse("10000000-0000-0000-0000-000000000006")
    };

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var users = UserIds.Select(pair => new User
        {
            Id = pair.Value,
            Email = $"dev-{pair.Key.ToString().ToLowerInvariant()}@example.test",
            NormalizedEmail = $"DEV-{pair.Key.ToString().ToUpperInvariant()}@EXAMPLE.TEST",
            FullName = $"Development {pair.Key}",
            Role = pair.Key,
            IsActive = pair.Key != UserRole.Blocked,
            BankAccountNumber = pair.Key == UserRole.User ? "DEV-ACCOUNT-001" : null,
            BankBranchCode = pair.Key == UserRole.User ? "DEV-001" : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ToArray();
        db.Users.AddRange(users);

        var client = new CollectionClient
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Name = "Development Collection Client",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var purpose = new CollectionPurposeOption { Id = 101, CollectionClientId = client.Id, Name = "Membership fee", DisplayOrder = 1 };
        var amount = new CollectionAmountOption { Id = 101, CollectionClientId = client.Id, Name = "Standard amount", Amount = 2500.00m, DisplayOrder = 1 };
        db.AddRange(client, purpose, amount,
            new CollectionClientUser { CollectionClientId = client.Id, UserId = UserIds[UserRole.User], AssignedAtUtc = now },
            new CollectionClientBankDetail { CollectionClientId = client.Id, AccountName = "Development Client", BankName = "Example Bank", BranchCode = "DEV-001", AccountNumber = "DEV-CLIENT-001", CreatedAtUtc = now });

        var draftClaim = new Claim { Id = 101, ClaimantUserId = UserIds[UserRole.User], Title = "Development mileage", Description = "Sample draft claim", Amount = 1200.00m, Status = ClaimStatus.Draft, CreatedAtUtc = now, UpdatedAtUtc = now };
        var acceptedClaim = new Claim { Id = 102, ClaimantUserId = UserIds[UserRole.User], Title = "Development supplies", Description = "Sample accepted claim", Amount = 3400.00m, Status = ClaimStatus.Accepted, PaymentStatus = ClaimPaymentStatus.Processing, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.AddRange(draftClaim, acceptedClaim,
            new ClaimComment { ClaimId = 101, AuthorUserId = UserIds[UserRole.User], Content = "Sample claimant comment", CreatedAtUtc = now },
            new ClaimComment { ClaimId = 102, AuthorUserId = UserIds[UserRole.Manager], Content = "Sample management comment", IsPrivate = true, CreatedAtUtc = now });

        var collection = new CollectionTransaction
        {
            Id = 101, CollectionClientId = client.Id, PurposeOptionId = purpose.Id, AmountOptionId = amount.Id,
            TellerUserId = UserIds[UserRole.Teller], PayorName = "Development Payor", PayorEmail = "payor@example.test",
            ReferenceNumber = "DEV-COL-001", Method = CollectionMethod.Pos, Amount = amount.Amount, ProcessingFee = 25.00m,
            PaymentDateUtc = now, CreatedAtUtc = now
        };
        db.Add(collection);
        await db.SaveChangesAsync(cancellationToken);

        var salary = new SalaryDefinition
        {
            UserId = UserIds[UserRole.User], Description = "Development monthly salary", BaseAmount = 85000.00m,
            FirstSalaryDate = DateOnly.FromDateTime(now.AddMonths(-1)), LastSalaryDate = DateOnly.FromDateTime(now.AddMonths(-1)),
            StartDate = DateOnly.FromDateTime(now.AddMonths(-3)), RecurrenceMonths = 1, NearestWeekday = DayOfWeek.Friday,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.SalaryDefinitions.Add(salary);
        await db.SaveChangesAsync(cancellationToken);

        var payroll = new Payroll
        {
            SalaryDefinitionId = salary.Id, UserId = UserIds[UserRole.User], PeriodEndingDate = DateOnly.FromDateTime(now),
            Description = "Development payroll", PayrollTotal = 87000.00m, Status = PayrollStatus.Generated, GeneratedAtUtc = now
        };
        db.AddRange(new SalaryAdjustment { SalaryDefinitionId = salary.Id, Title = "Travel benefit", FixedValue = 2000.00m, Type = SalaryAdjustmentType.Benefit }, payroll);
        await db.SaveChangesAsync(cancellationToken);
        db.PayrollEntries.AddRange(
            new PayrollEntry { PayrollId = payroll.Id, Description = "Base salary", Amount = 85000.00m, Type = PayrollEntryType.Base, IsLocked = true, SortOrder = 0, CreatedAtUtc = now },
            new PayrollEntry { PayrollId = payroll.Id, Description = "Travel benefit", Amount = 2000.00m, Type = PayrollEntryType.Benefit, IsLocked = true, SortOrder = 1, CreatedAtUtc = now });

        var claimJob = new JobPayment { Id = 101, PayeeUserId = UserIds[UserRole.User], Status = JobPaymentStatus.Processing, JobTotal = acceptedClaim.Amount, TotalPaid = acceptedClaim.Amount, PublicNote = "Development claim payment", CreatedAtUtc = now };
        var collectionJob = new JobPayment { Id = 102, CollectionClientId = client.Id, Status = JobPaymentStatus.Processing, JobTotal = collection.Amount, ClientProcessingFee = collection.ProcessingFee, TotalPaid = collection.Amount - collection.ProcessingFee, PublicNote = "Development collection payment", CreatedAtUtc = now };
        db.AddRange(claimJob, collectionJob,
            new JobPaymentClaim { JobPaymentId = claimJob.Id, ClaimId = acceptedClaim.Id },
            new JobPaymentCollection { JobPaymentId = collectionJob.Id, CollectionTransactionId = collection.Id });

        var outboxId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        db.AddRange(
            new EmailOutboxItem { Id = outboxId, Recipient = "recipient@example.test", Subject = "Development receipt", HtmlBody = "<p>Development receipt</p>", RelatedEntityType = "CollectionTransaction", RelatedEntityId = collection.Id.ToString(), IdempotencyKey = "development-receipt-101", CreatedAtUtc = now, AvailableAtUtc = now },
            new EmailLog { OutboxItemId = outboxId, Recipient = "recipient@example.test", Subject = "Development receipt", HtmlBody = "<p>Development receipt</p>", Provider = "Development", RelatedEntityType = "CollectionTransaction", RelatedEntityId = collection.Id.ToString(), AttemptNumber = 1, Status = EmailOutboxStatus.Pending, CreatedAtUtc = now },
            new OAuthClient { ClientId = "development-client", ClientName = "Development sample client", ClientSecretHash = "development-only-not-a-secret", RedirectUrisJson = "[\"https://example.test/callback\"]", CreatedAtUtc = now },
            new AuditRecord { ActorUserId = UserIds[UserRole.Administrator].ToString(), ActorEmail = "dev-administrator@example.test", Action = "DevelopmentDataSeeded", Target = "DevelopmentData", IsMcpOperation = false, TimestampUtc = now });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded development-only in-memory sample data for {UserCount} roles.", users.Length);
    }
}
