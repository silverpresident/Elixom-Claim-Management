using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Lib.Tests.Data;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDatabaseMigrationsAsync_WhenNonRelational_SkipsMigrateAndSeedsAdmin()
    {
        var dbName = $"MigrationTest_{Guid.NewGuid()}";
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.Configure<DatabaseOptions>(options =>
        {
            options.ClaimDatabase = "Server=test;Database=test;";
            options.AutoApplyMigrations = true;
        });

        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAdminEmail = "admin@example.com";
        });

        var provider = services.BuildServiceProvider();

        // Should not throw when run against InMemory provider because IsRelational() is false
        await provider.ApplyDatabaseMigrationsAsync();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@example.com");

        Assert.NotNull(admin);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task ApplyDatabaseMigrationsAsync_WhenAutoApplyDisabled_SkipsMigrateAndSeedsAdmin()
    {
        var dbName = $"MigrationTestDisabled_{Guid.NewGuid()}";
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.Configure<DatabaseOptions>(options =>
        {
            options.ClaimDatabase = "Server=test;Database=test;";
            options.AutoApplyMigrations = false;
        });

        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAdminEmail = "disabled-admin@example.com";
        });

        var provider = services.BuildServiceProvider();

        await provider.ApplyDatabaseMigrationsAsync();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "disabled-admin@example.com");

        Assert.NotNull(admin);
        Assert.Equal(UserRole.Administrator, admin.Role);
    }
}
