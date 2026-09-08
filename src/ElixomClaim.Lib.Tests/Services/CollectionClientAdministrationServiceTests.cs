using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class CollectionClientAdministrationServiceTests
{
    [Fact]
    public async Task CreateAndUpdateClientAsync_AllowsAccountantAndDeniesManager()
    {
        await using var db = CreateDb();
        var manager = new User { Email = "manager@anonymized.example.com", NormalizedEmail = "MANAGER@ANONYMIZED.EXAMPLE.COM", FullName = "Manager", Role = UserRole.Manager };
        var accountant = new User { Email = "accountant@anonymized.example.com", NormalizedEmail = "ACCOUNTANT@ANONYMIZED.EXAMPLE.COM", FullName = "Accountant", Role = UserRole.Accountant };
        db.Users.AddRange(manager, accountant);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var rejected = await service.CreateClientAsync(new(manager.Id, "Acme"));
        var created = await service.CreateClientAsync(new(accountant.Id, "Acme"));
        var updated = await service.UpdateClientAsync(new(accountant.Id, created.Value!.Id, "Acme Utilities", "Utility collections", "Accounting note", 25m, 2.5m));

        Assert.True(rejected.IsFailure);
        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.Equal("Acme Utilities", updated.Value!.Name);
        Assert.Equal(25m, updated.Value.PerJobProcessingFee);
        Assert.Contains(db.AuditRecords, record => record.Action == "COLLECTION_CLIENT_CREATED");
        Assert.Contains(db.AuditRecords, record => record.Action == "COLLECTION_CLIENT_UPDATED");
    }

    [Fact]
    public async Task AddAmountOptionAsync_RejectsNonPositiveAmounts()
    {
        await using var db = CreateDb();
        var admin = new User { Email = "admin@anonymized.example.com", NormalizedEmail = "ADMIN@ANONYMIZED.EXAMPLE.COM", FullName = "Admin", Role = UserRole.Administrator };
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(admin, client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).AddAmountOptionAsync(new(admin.Id, client.Id, "Invalid", 0m, 0));

        Assert.True(result.IsFailure);
        Assert.Empty(db.CollectionAmountOptions);
    }

    [Fact]
    public async Task AddAmountOptionAsync_RemainsAdministratorOnly()
    {
        await using var db = CreateDb();
        var accountant = new User { Email = "accountant@anonymized.example.com", NormalizedEmail = "ACCOUNTANT@ANONYMIZED.EXAMPLE.COM", FullName = "Accountant", Role = UserRole.Accountant };
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(accountant, client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).AddAmountOptionAsync(new(accountant.Id, client.Id, "Standard", 100m, 0));

        Assert.True(result.IsFailure);
        Assert.Empty(db.CollectionAmountOptions);
    }

    [Fact]
    public async Task SetClientActiveAsync_AllowsAccountantAndAuditsLifecycleChange()
    {
        await using var db = CreateDb();
        var accountant = new User { Email = "accountant@anonymized.example.com", NormalizedEmail = "ACCOUNTANT@ANONYMIZED.EXAMPLE.COM", FullName = "Accountant", Role = UserRole.Accountant };
        var client = new CollectionClient { Name = "Acme", IsActive = true };
        db.AddRange(accountant, client);
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetClientActiveAsync(new(accountant.Id, client.Id, false));

        Assert.True(result.IsSuccess);
        Assert.False(client.IsActive);
        Assert.Contains(db.AuditRecords, record => record.Action == "COLLECTION_CLIENT_DISABLED");
    }

    [Fact]
    public async Task AddBankDetailAsync_RequiresBranchNameAndApprovedAccountType()
    {
        await using var db = CreateDb();
        var admin = new User { Email = "admin@anonymized.example.com", NormalizedEmail = "ADMIN@ANONYMIZED.EXAMPLE.COM", FullName = "Admin", Role = UserRole.Administrator };
        var client = new CollectionClient { Name = "Acme" };
        db.AddRange(admin, client);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var missingBranch = await service.AddBankDetailAsync(new(admin.Id, client.Id, "Acme", "Example Bank", "001", "", CollectionBankAccountTypes.Savings, "123456"));
        var invalidType = await service.AddBankDetailAsync(new(admin.Id, client.Id, "Acme", "Example Bank", "001", "Kingston", "Deposit", "123456"));
        var created = await service.AddBankDetailAsync(new(admin.Id, client.Id, "Acme", "Example Bank", "001", "Kingston", CollectionBankAccountTypes.Current, "123456"));

        Assert.True(missingBranch.IsFailure);
        Assert.True(invalidType.IsFailure);
        Assert.True(created.IsSuccess);
        Assert.Equal("Kingston", created.Value!.BranchName);
        Assert.Equal(CollectionBankAccountTypes.Current, created.Value.AccountType);
        Assert.Contains(db.AuditRecords, record => record.Action == "COLLECTION_CLIENT_BANK_DETAIL_ADDED");
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CollectionClientAdministrationService CreateService(ApplicationDbContext db) => new(
        db,
        new AuditService(db, NullLogger<AuditService>.Instance),
        new SystemClock(),
        NullLogger<CollectionClientAdministrationService>.Instance);
}
