using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ElixomClaim.Lib.Data;

public class ApplicationDbContext : DbContext
{
    public const string DefaultSchema = "dbclaim";

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<OAuthConsent> OAuthConsents => Set<OAuthConsent>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimComment> ClaimComments => Set<ClaimComment>();
    public DbSet<CollectionClient> CollectionClients => Set<CollectionClient>();
    public DbSet<CollectionClientUser> CollectionClientUsers => Set<CollectionClientUser>();
    public DbSet<CollectionClientBankDetail> CollectionClientBankDetails => Set<CollectionClientBankDetail>();
    public DbSet<CollectionPurposeOption> CollectionPurposeOptions => Set<CollectionPurposeOption>();
    public DbSet<CollectionAmountOption> CollectionAmountOptions => Set<CollectionAmountOption>();
    public DbSet<CollectionTransaction> CollectionTransactions => Set<CollectionTransaction>();
    public DbSet<EmailOutboxItem> EmailOutboxItems => Set<EmailOutboxItem>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<SalaryDefinition> SalaryDefinitions => Set<SalaryDefinition>();
    public DbSet<SalaryAdjustment> SalaryAdjustments => Set<SalaryAdjustment>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<JobPayment> JobPayments => Set<JobPayment>();
    public DbSet<JobPaymentClaim> JobPaymentClaims => Set<JobPaymentClaim>();
    public DbSet<JobPaymentCollection> JobPaymentCollections => Set<JobPaymentCollection>();
    public DbSet<JobPaymentPayroll> JobPaymentPayrolls => Set<JobPaymentPayroll>();
    public DbSet<JobPaymentDeduction> JobPaymentDeductions => Set<JobPaymentDeduction>();
    public DbSet<OperationRecord> OperationRecords => Set<OperationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enforce Azure SQL schema 'dbclaim'
        modelBuilder.HasDefaultSchema(DefaultSchema);

        // Global soft-delete query filters
        modelBuilder.Entity<Claim>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<ClaimComment>().HasQueryFilter(cc => !cc.IsDeleted);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("IX_Users_NormalizedEmail");

            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(u => u.Role)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(u => u.BankAccountNumber)
                .HasMaxLength(100);

            entity.Property(u => u.BankBranchCode)
                .HasMaxLength(50);

            entity.Property(u => u.CreatedAtUtc)
                .IsRequired();

            entity.Property(u => u.UpdatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("AuditRecords");

            entity.HasKey(a => a.Id);

            entity.Property(a => a.ActorUserId)
                .HasMaxLength(450);

            entity.Property(a => a.ActorEmail)
                .HasMaxLength(256);

            entity.Property(a => a.CorrelationId)
                .HasMaxLength(100);

            entity.Property(a => a.IpAddress)
                .HasMaxLength(50);

            entity.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Target)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.IsMcpOperation)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(a => a.TimestampUtc)
                .IsRequired();
        });

        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.ToTable("OAuthClients");
            entity.HasKey(c => c.ClientId);
            entity.Property(c => c.ClientId).HasMaxLength(100);
            entity.Property(c => c.ClientName).IsRequired().HasMaxLength(200);
            entity.Property(c => c.ClientSecretHash).IsRequired().HasMaxLength(256);
            entity.Property(c => c.RedirectUrisJson).IsRequired().HasMaxLength(2000);
            entity.Property(c => c.AllowedGrantTypes).IsRequired().HasMaxLength(200);
            entity.Property(c => c.AllowedScopes).IsRequired().HasMaxLength(500);
            entity.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(c => c.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<OAuthAuthorizationCode>(entity =>
        {
            entity.ToTable("OAuthAuthorizationCodes");
            entity.HasKey(c => c.CodeHash);
            entity.Property(c => c.CodeHash).HasMaxLength(256);
            entity.Property(c => c.ClientId).IsRequired().HasMaxLength(100);
            entity.Property(c => c.UserId).IsRequired().HasMaxLength(450);
            entity.Property(c => c.RedirectUri).IsRequired().HasMaxLength(2000);
            entity.Property(c => c.Scope).IsRequired().HasMaxLength(500);
            entity.Property(c => c.CodeChallenge).IsRequired().HasMaxLength(256);
            entity.Property(c => c.CodeChallengeMethod).IsRequired().HasMaxLength(10);
            entity.Property(c => c.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(c => c.ExpiresAtUtc).IsRequired();
            entity.Property(c => c.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<OAuthConsent>(entity =>
        {
            entity.ToTable("OAuthConsents");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(100);
            entity.Property(c => c.UserId).IsRequired().HasMaxLength(450);
            entity.Property(c => c.ClientId).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Scope).IsRequired().HasMaxLength(500);
            entity.Property(c => c.GrantedAtUtc).IsRequired();
            entity.HasIndex(c => new { c.UserId, c.ClientId }).IsUnique();
        });

        modelBuilder.Entity<OAuthToken>(entity =>
        {
            entity.ToTable("OAuthTokens");
            entity.HasKey(t => t.TokenHash);
            entity.Property(t => t.TokenHash).HasMaxLength(256);
            entity.Property(t => t.TokenId).IsRequired().HasMaxLength(100);
            entity.Property(t => t.TokenType).IsRequired().HasMaxLength(50);
            entity.Property(t => t.ClientId).IsRequired().HasMaxLength(100);
            entity.Property(t => t.UserId).IsRequired().HasMaxLength(450);
            entity.Property(t => t.Scope).IsRequired().HasMaxLength(500);
            entity.Property(t => t.RefreshTokenFamilyId).HasMaxLength(100);
            entity.Property(t => t.IsRevoked).IsRequired().HasDefaultValue(false);
            entity.Property(t => t.ExpiresAtUtc).IsRequired();
            entity.Property(t => t.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.ToTable("Claims");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(4000);
            entity.Property(c => c.Amount).IsRequired().HasPrecision(18, 2);
            entity.Property(c => c.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("JMD");
            entity.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(c => c.PaymentStatus).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(c => c.RejectionReason).HasMaxLength(1000);
            entity.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.Property(c => c.RowVersion).IsRowVersion();

            entity.HasOne(c => c.ClaimantUser)
                .WithMany()
                .HasForeignKey(c => c.ClaimantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.ClaimantUserId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.PaymentStatus);
        });

        modelBuilder.Entity<ClaimComment>(entity =>
        {
            entity.ToTable("ClaimComments");
            entity.HasKey(cc => cc.Id);
            entity.Property(cc => cc.Content).IsRequired().HasMaxLength(4000);
            entity.Property(cc => cc.IsPrivate).IsRequired().HasDefaultValue(false);
            entity.Property(cc => cc.IsDeleted).IsRequired().HasDefaultValue(false);

            entity.HasOne(cc => cc.Claim)
                .WithMany(c => c.Comments)
                .HasForeignKey(cc => cc.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cc => cc.AuthorUser)
                .WithMany()
                .HasForeignKey(cc => cc.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cc => cc.ClaimId);
        });

        modelBuilder.Entity<CollectionClient>(entity =>
        {
            entity.ToTable("CollectionClients");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<CollectionClientUser>(entity =>
        {
            entity.ToTable("CollectionClientUsers");
            entity.HasKey(cu => new { cu.CollectionClientId, cu.UserId });
            entity.HasOne(cu => cu.CollectionClient).WithMany(c => c.AssignedUsers)
                .HasForeignKey(cu => cu.CollectionClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(cu => cu.User).WithMany().HasForeignKey(cu => cu.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(cu => cu.UserId);
        });

        modelBuilder.Entity<CollectionClientBankDetail>(entity =>
        {
            entity.ToTable("CollectionClientBankDetails");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.AccountName).IsRequired().HasMaxLength(200);
            entity.Property(b => b.BankName).IsRequired().HasMaxLength(200);
            entity.Property(b => b.BranchCode).IsRequired().HasMaxLength(50);
            entity.Property(b => b.AccountNumber).IsRequired().HasMaxLength(100);
            entity.Property(b => b.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasOne(b => b.CollectionClient).WithMany(c => c.BankDetails)
                .HasForeignKey(b => b.CollectionClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(b => new { b.CollectionClientId, b.IsActive });
        });

        modelBuilder.Entity<CollectionPurposeOption>(entity =>
        {
            entity.ToTable("CollectionPurposeOptions");
            entity.HasKey(o => o.Id);
            entity.HasAlternateKey(o => new { o.Id, o.CollectionClientId });
            entity.Property(o => o.Name).IsRequired().HasMaxLength(200);
            entity.Property(o => o.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasOne(o => o.CollectionClient).WithMany(c => c.PurposeOptions)
                .HasForeignKey(o => o.CollectionClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(o => new { o.CollectionClientId, o.Name }).IsUnique();
        });

        modelBuilder.Entity<CollectionAmountOption>(entity =>
        {
            entity.ToTable("CollectionAmountOptions");
            entity.HasKey(o => o.Id);
            entity.HasAlternateKey(o => new { o.Id, o.CollectionClientId });
            entity.Property(o => o.Name).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Amount).IsRequired().HasPrecision(18, 2);
            entity.Property(o => o.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasOne(o => o.CollectionClient).WithMany(c => c.AmountOptions)
                .HasForeignKey(o => o.CollectionClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(o => new { o.CollectionClientId, o.Name }).IsUnique();
        });

        modelBuilder.Entity<CollectionTransaction>(entity =>
        {
            entity.ToTable("CollectionTransactions");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.PayorName).IsRequired().HasMaxLength(200);
            entity.Property(c => c.PayorEmail).HasMaxLength(256);
            entity.Property(c => c.ReferenceNumber).HasMaxLength(100);
            entity.Property(c => c.Method).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(c => c.Amount).IsRequired().HasPrecision(18, 2);
            entity.Property(c => c.ProcessingFee).IsRequired().HasPrecision(18, 2);
            entity.Property(c => c.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("JMD");
            entity.Property(c => c.RowVersion).IsRowVersion();
            entity.HasOne(c => c.CollectionClient).WithMany().HasForeignKey(c => c.CollectionClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.PurposeOption).WithMany().HasForeignKey(c => new { c.PurposeOptionId, c.CollectionClientId })
                .HasPrincipalKey(o => new { o.Id, o.CollectionClientId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.AmountOption).WithMany().HasForeignKey(c => new { c.AmountOptionId, c.CollectionClientId })
                .HasPrincipalKey(o => new { o.Id, o.CollectionClientId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.TellerUser).WithMany().HasForeignKey(c => c.TellerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(c => new { c.CollectionClientId, c.Status, c.PaymentDateUtc });
            entity.HasIndex(c => new { c.TellerUserId, c.CreatedAtUtc });
        });

        modelBuilder.Entity<EmailOutboxItem>(entity =>
        {
            entity.ToTable("EmailOutboxItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Recipient).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(300);
            entity.Property(e => e.HtmlBody).IsRequired();
            entity.Property(e => e.RelatedEntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RelatedEntityId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.Status, e.AvailableAtUtc });
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("EmailLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Recipient).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(300);
            entity.Property(e => e.HtmlBody).IsRequired();
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RelatedEntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RelatedEntityId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.HasIndex(e => e.OutboxItemId);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        modelBuilder.Entity<SalaryDefinition>(entity =>
        {
            entity.ToTable("SalaryDefinitions", table =>
            {
                table.HasCheckConstraint("CK_SalaryDefinitions_BaseAmount", "[BaseAmount] > 0");
                table.HasCheckConstraint("CK_SalaryDefinitions_Recurrence", "[RecurrenceDays] >= 0 AND [RecurrenceMonths] >= 0 AND ([RecurrenceDays] > 0 OR [RecurrenceMonths] > 0)");
                table.HasCheckConstraint("CK_SalaryDefinitions_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                table.HasCheckConstraint("CK_SalaryDefinitions_NearestWeekday", "[NearestWeekday] >= 0 AND [NearestWeekday] <= 6");
            });
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Description).IsRequired().HasMaxLength(500);
            entity.Property(s => s.BaseAmount).IsRequired().HasPrecision(18, 2);
            entity.Property(s => s.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(s => s.RowVersion).IsRowVersion();
            entity.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(s => new { s.UserId, s.IsActive });
        });

        modelBuilder.Entity<SalaryAdjustment>(entity =>
        {
            entity.ToTable("SalaryAdjustments", table => table.HasCheckConstraint("CK_SalaryAdjustments_Range", "[PercentageRate] >= 0 AND [PercentageRate] <= 1 AND [FixedValue] >= 0"));
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Title).IsRequired().HasMaxLength(500);
            entity.Property(a => a.PercentageRate).IsRequired().HasPrecision(18, 3);
            entity.Property(a => a.FixedValue).IsRequired().HasPrecision(18, 2);
            entity.Property(a => a.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.HasOne(a => a.SalaryDefinition).WithMany(s => s.Adjustments).HasForeignKey(a => a.SalaryDefinitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(a => new { a.SalaryDefinitionId, a.Type });
        });

        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("Payrolls");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Description).IsRequired().HasMaxLength(500);
            entity.Property(p => p.PayrollTotal).IsRequired().HasPrecision(18, 2);
            entity.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(p => p.IsLocked).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.RowVersion).IsRowVersion();
            entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.SalaryDefinition).WithMany(s => s.Payrolls).HasForeignKey(p => p.SalaryDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(p => new { p.UserId, p.Status });
            entity.HasIndex(p => new { p.SalaryDefinitionId, p.PeriodEndingDate }).IsUnique();
        });

        modelBuilder.Entity<PayrollEntry>(entity =>
        {
            entity.ToTable("PayrollEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
            entity.Property(e => e.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.IsLocked).IsRequired().HasDefaultValue(false);
            entity.HasOne(e => e.Payroll).WithMany(p => p.Entries).HasForeignKey(e => e.PayrollId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.PayrollId, e.SortOrder }).IsUnique();
        });

        modelBuilder.Entity<JobPayment>(entity =>
        {
            entity.ToTable("JobPayments", table => table.HasCheckConstraint("CK_JobPayments_ExactlyOnePayee", "([PayeeUserId] IS NOT NULL AND [CollectionClientId] IS NULL) OR ([PayeeUserId] IS NULL AND [CollectionClientId] IS NOT NULL)"));
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(j => j.PublicNote).HasMaxLength(4000);
            entity.Property(j => j.InternalNote).HasMaxLength(4000);
            entity.Property(j => j.JobTotal).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.ClientProcessingFee).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.TotalTxnProcessingFee).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.TotalDeductions).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.TotalPaid).IsRequired().HasPrecision(18, 2);
            entity.Property(j => j.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("JMD");
            entity.Property(j => j.PaymentTransactionNumber).HasMaxLength(100);
            entity.Property(j => j.AdjustmentReason).HasMaxLength(1000);
            entity.Property(j => j.RowVersion).IsRowVersion();
            entity.HasOne(j => j.PayeeUser).WithMany().HasForeignKey(j => j.PayeeUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(j => j.CollectionClient).WithMany().HasForeignKey(j => j.CollectionClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(j => j.OriginalJobPayment).WithMany().HasForeignKey(j => j.OriginalJobPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(j => new { j.Status, j.ScheduledAtUtc });
            entity.HasIndex(j => j.PayeeUserId);
            entity.HasIndex(j => j.CollectionClientId);
        });

        modelBuilder.Entity<JobPaymentClaim>(entity =>
        {
            entity.ToTable("JobPaymentClaims"); entity.HasKey(x => new { x.JobPaymentId, x.ClaimId });
            entity.HasOne(x => x.JobPayment).WithMany(j => j.Claims).HasForeignKey(x => x.JobPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Claim).WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ClaimId).IsUnique();
        });
        modelBuilder.Entity<JobPaymentCollection>(entity =>
        {
            entity.ToTable("JobPaymentCollections"); entity.HasKey(x => new { x.JobPaymentId, x.CollectionTransactionId });
            entity.HasOne(x => x.JobPayment).WithMany(j => j.Collections).HasForeignKey(x => x.JobPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CollectionTransaction).WithMany().HasForeignKey(x => x.CollectionTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CollectionTransactionId).IsUnique();
        });
        modelBuilder.Entity<JobPaymentPayroll>(entity =>
        {
            entity.ToTable("JobPaymentPayrolls"); entity.HasKey(x => new { x.JobPaymentId, x.PayrollId });
            entity.HasOne(x => x.JobPayment).WithMany(j => j.Payrolls).HasForeignKey(x => x.JobPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Payroll).WithMany().HasForeignKey(x => x.PayrollId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PayrollId).IsUnique();
        });
        modelBuilder.Entity<JobPaymentDeduction>(entity =>
        {
            entity.ToTable("JobPaymentDeductions"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).IsRequired().HasMaxLength(500);
            entity.Property(x => x.Amount).IsRequired().HasPrecision(18, 2);
            entity.HasOne(x => x.JobPayment).WithMany(j => j.Deductions).HasForeignKey(x => x.JobPaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.JobPaymentId);
        });

        modelBuilder.Entity<OperationRecord>(entity =>
        {
            entity.ToTable("OperationRecords");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.IdempotencyKey).IsRequired().HasMaxLength(250);
            entity.Property(o => o.OperationType).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Status).IsRequired().HasMaxLength(50);
            entity.Property(o => o.Details).HasMaxLength(2000);
            entity.Property(o => o.ActorUserId).IsRequired().HasMaxLength(450);
            entity.Property(o => o.ExecutedAtUtc).IsRequired();
            entity.HasIndex(o => o.IdempotencyKey).IsUnique();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // All decimal properties default to decimal(18,2) for exact JMD currency values
        configurationBuilder
            .Properties<decimal>()
            .HavePrecision(18, 2);

        // Ensure DateTime values are converted to UTC when stored and retrieved as UTC
        configurationBuilder
            .Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder
            .Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }
}

internal class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

internal class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
