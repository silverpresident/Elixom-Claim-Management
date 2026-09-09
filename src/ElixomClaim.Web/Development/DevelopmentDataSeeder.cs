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
            await AddRunCollectionTransactionAsync(db, cancellationToken);
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
            Description = "Development collection client for local testing",
            Notes = "Internal management notes for development client",
            PerJobProcessingFee = 25.00m,
            PerTransactionFee = 5.00m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var purpose = new CollectionPurposeOption { Id = Guid.Parse("40000000-0000-0000-0000-000000000101"), CollectionClientId = client.Id, Name = "Membership fee", DisplayOrder = 1 };
        var amount = new CollectionAmountOption { Id = Guid.Parse("40000000-0000-0000-0000-000000000102"), CollectionClientId = client.Id, Name = "Standard amount", Amount = 2500.00m, DisplayOrder = 1 };
        db.AddRange(client, purpose, amount,
            new CollectionClientUser { CollectionClientId = client.Id, UserId = UserIds[UserRole.User], AssignedAtUtc = now },
            new CollectionClientBankDetail { Id = Guid.Parse("21000000-0000-0000-0000-000000000001"), CollectionClientId = client.Id, AccountName = "Development Client", BankName = "Example Bank", BranchCode = "DEV-001", BranchName = "Development Branch", AccountType = CollectionBankAccountTypes.Current, AccountNumber = "DEV-CLIENT-001", CreatedAtUtc = now });

        var draftClaim = new Claim { Id = Guid.Parse("50000000-0000-0000-0000-000000000101"), ClaimantUserId = UserIds[UserRole.User], Title = "Development mileage", Description = "Sample draft claim", DateOfJob = now, Amount = 1200.00m, Status = ClaimStatus.Draft, CreatedAtUtc = now, UpdatedAtUtc = now };
        var acceptedClaim = new Claim { Id = Guid.Parse("50000000-0000-0000-0000-000000000102"), ClaimantUserId = UserIds[UserRole.User], Title = "Development supplies", Description = "Sample accepted claim", DateOfJob = now, Amount = 3400.00m, Status = ClaimStatus.Accepted, PaymentStatus = ClaimPaymentStatus.Processing, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.AddRange(draftClaim, acceptedClaim,
            new ClaimComment { Id = Guid.Parse("51000000-0000-0000-0000-000000000101"), ClaimId = draftClaim.Id, AuthorUserId = UserIds[UserRole.User], Content = "Sample claimant comment", CreatedAtUtc = now },
            new ClaimComment { Id = Guid.Parse("51000000-0000-0000-0000-000000000102"), ClaimId = acceptedClaim.Id, AuthorUserId = UserIds[UserRole.Manager], Content = "Sample management comment", IsPrivate = true, CreatedAtUtc = now });

        var collection = new CollectionTransaction
        {
            Id = Guid.Parse("60000000-0000-0000-0000-000000000101"),
            CollectionClientId = client.Id,
            PurposeOptionId = purpose.Id,
            Purpose = purpose.Name,
            AmountOptionId = amount.Id,
            TellerUserId = UserIds[UserRole.Teller],
            PayorName = "Development Payor",
            PayorEmail = "payor@example.test",
            PayorTelephone = "8765550100",
            ReferenceNumber = "DEV-COL-001",
            Method = CollectionMethod.Pos,
            Amount = amount.Amount,
            ProcessingFee = 25.00m,
            PaymentDateUtc = now,
            CreatedAtUtc = now
        };
        db.Add(collection);
        await db.SaveChangesAsync(cancellationToken);

        var salary = new SalaryDefinition
        {
            Id = Guid.Parse("70000000-0000-0000-0000-000000000101"),
            UserId = UserIds[UserRole.User],
            Description = "Development monthly salary",
            BaseAmount = 85000.00m,
            FirstSalaryDate = DateOnly.FromDateTime(now.AddMonths(-1)),
            LastSalaryDate = DateOnly.FromDateTime(now.AddMonths(-1)),
            StartDate = DateOnly.FromDateTime(now.AddMonths(-3)),
            RecurrenceMonths = 1,
            NearestWeekday = DayOfWeek.Friday,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.SalaryDefinitions.Add(salary);
        await db.SaveChangesAsync(cancellationToken);

        var payroll = new Payroll
        {
            Id = Guid.Parse("80000000-0000-0000-0000-000000000101"),
            SalaryDefinitionId = salary.Id,
            UserId = UserIds[UserRole.User],
            PeriodEndingDate = DateOnly.FromDateTime(now),
            Description = "Development payroll",
            PayrollTotal = 87000.00m,
            Status = PayrollStatus.Generated,
            GeneratedAtUtc = now
        };
        db.AddRange(new SalaryAdjustment { Id = Guid.Parse("71000000-0000-0000-0000-000000000101"), SalaryDefinitionId = salary.Id, Title = "Travel benefit", FixedValue = 2000.00m, Type = SalaryAdjustmentType.Benefit }, payroll);
        await db.SaveChangesAsync(cancellationToken);
        db.PayrollEntries.AddRange(
            new PayrollEntry { Id = Guid.Parse("81000000-0000-0000-0000-000000000101"), PayrollId = payroll.Id, Description = "Base salary", Amount = 85000.00m, Type = PayrollEntryType.Base, IsLocked = true, SortOrder = 0, CreatedAtUtc = now },
            new PayrollEntry { Id = Guid.Parse("81000000-0000-0000-0000-000000000102"), PayrollId = payroll.Id, Description = "Travel benefit", Amount = 2000.00m, Type = PayrollEntryType.Benefit, IsLocked = true, SortOrder = 1, CreatedAtUtc = now });

        var claimJob = new JobPayment
        {
            Id = Guid.Parse("90000000-0000-0000-0000-000000000101"),
            PayeeUserId = UserIds[UserRole.User],
            Status = JobPaymentStatus.Processing,
            JobTotal = acceptedClaim.Amount,
            TotalPaid = acceptedClaim.Amount,
            PublicNote = "Development claim payment",
            CreatedAtUtc = now,
            PayoutBankName = "Development Bank",
            PayoutBankBranchCode = "DEV-001",
            PayoutBankAccountNumber = "DEV-ACCOUNT-001",
            PayoutBankAccountName = "Development User"
        };
        var collectionJob = new JobPayment
        {
            Id = Guid.Parse("90000000-0000-0000-0000-000000000102"),
            CollectionClientId = client.Id,
            Status = JobPaymentStatus.Processing,
            JobTotal = collection.Amount,
            ClientProcessingFee = collection.ProcessingFee,
            TotalPaid = collection.Amount - collection.ProcessingFee,
            PublicNote = "Development collection payment",
            CreatedAtUtc = now,
            PayoutBankName = "Example Bank",
            PayoutBankBranchCode = "DEV-001",
            PayoutBankAccountNumber = "DEV-CLIENT-001",
            PayoutBankAccountName = "Development Client"
        };
        db.AddRange(claimJob, collectionJob,
            new JobPaymentClaim { JobPaymentId = claimJob.Id, ClaimId = acceptedClaim.Id },
            new JobPaymentCollection { JobPaymentId = collectionJob.Id, CollectionTransactionId = collection.Id });

        var outboxId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        db.AddRange(
            new EmailOutboxItem { Id = outboxId, To = "recipient@example.test", From = "no-reply@example.test", Bcc = "operations@example.test", Subject = "Development receipt", HtmlBody = "<p>Development receipt</p>", RelatedEntityType = "CollectionTransaction", RelatedEntityId = collection.Id.ToString(), IdempotencyKey = "development-receipt-101", CreatedAtUtc = now, AvailableAtUtc = now },
            new EmailLog { Id = Guid.Parse("31000000-0000-0000-0000-000000000001"), OutboxItemId = outboxId, To = "recipient@example.test", From = "no-reply@example.test", Bcc = "operations@example.test", Subject = "Development receipt", HtmlBody = "<p>Development receipt</p>", Provider = "Development", RelatedEntityType = "CollectionTransaction", RelatedEntityId = collection.Id.ToString(), AttemptNumber = 1, Status = EmailOutboxStatus.Pending, CreatedAtUtc = now },
            new OAuthClient { ClientId = "development-client", ClientName = "Development sample client", ClientType = OAuthClientType.Public, RedirectUrisJson = "[\"https://example.test/callback\"]", CreatedAtUtc = now },
            new AuditRecord { Id = Guid.Parse("11000000-0000-0000-0000-000000000001"), ActorUserId = UserIds[UserRole.Administrator].ToString(), ActorEmail = "dev-administrator@example.test", Action = "DevelopmentDataSeeded", EntityType = "DevelopmentData", EntityId = "Seed", IsMcpOperation = false, OccurredAtUtc = now });

        await db.SaveChangesAsync(cancellationToken);
        await AddRunCollectionTransactionAsync(db, cancellationToken);
        logger.LogInformation("Seeded development-only in-memory sample data for {UserCount} roles.", users.Length);
    }

    private static async Task AddRunCollectionTransactionAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var client = await db.CollectionClients
            .SingleAsync(candidate => candidate.Id == Guid.Parse("20000000-0000-0000-0000-000000000001"), cancellationToken);
        var purpose = await db.CollectionPurposeOptions
            .SingleAsync(option => option.CollectionClientId == client.Id && option.Name == "Membership fee", cancellationToken);
        var amount = await db.CollectionAmountOptions
            .SingleAsync(option => option.CollectionClientId == client.Id && option.Name == "Standard amount", cancellationToken);
        var now = DateTime.UtcNow;

        db.CollectionTransactions.Add(new CollectionTransaction
        {
            Id = Guid.NewGuid(),
            CollectionClientId = client.Id,
            PurposeOptionId = purpose.Id,
            Purpose = purpose.Name,
            AmountOptionId = amount.Id,
            TellerUserId = UserIds[UserRole.Teller],
            PayorName = "Development walk-in payor",
            ReferenceNumber = $"DEV-RUN-{now:yyyyMMddHHmmssfff}",
            Method = CollectionMethod.Cash,
            Status = CollectionStatus.Collected,
            Amount = amount.Amount,
            ProcessingFee = client.PerTransactionFee,
            PaymentDateUtc = now,
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
