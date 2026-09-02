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
    public async Task CreateClientAsync_RequiresAdministratorAndAuditsSuccessfulConfiguration()
    {
        await using var db = CreateDb();
        var user = new User { Email = "user@anonymized.example.com", NormalizedEmail = "USER@ANONYMIZED.EXAMPLE.COM", FullName = "User", Role = UserRole.User };
        var admin = new User { Email = "admin@anonymized.example.com", NormalizedEmail = "ADMIN@ANONYMIZED.EXAMPLE.COM", FullName = "Admin", Role = UserRole.Administrator };
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var rejected = await service.CreateClientAsync(new(user.Id, "Acme"));
        var created = await service.CreateClientAsync(new(admin.Id, "Acme"));

        Assert.True(rejected.IsFailure);
        Assert.True(created.IsSuccess);
        Assert.Equal("Acme", created.Value!.Name);
        Assert.Contains(db.AuditRecords, record => record.Action == "COLLECTION_CLIENT_CREATED");
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

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CollectionClientAdministrationService CreateService(ApplicationDbContext db) => new(
        db,
        new AuditService(db, NullLogger<AuditService>.Instance),
        new SystemClock(),
        NullLogger<CollectionClientAdministrationService>.Instance);
}
