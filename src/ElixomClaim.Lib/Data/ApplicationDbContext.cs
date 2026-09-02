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
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimComment> ClaimComments => Set<ClaimComment>();

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
            entity.Property(c => c.Code).HasMaxLength(256);
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
