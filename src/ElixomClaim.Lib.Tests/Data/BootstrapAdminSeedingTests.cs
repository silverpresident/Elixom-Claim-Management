using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class BootstrapAdminSeedingTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SeedBootstrapAdminAsync_SeedsNewAdministrator_WhenNoUserExists()
    {
        using var dbContext = CreateInMemoryDbContext();
        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton(Options.Create(new AuthenticationOptions
        {
            DefaultAdminEmail = "admin@elixom.com"
        }));
        var serviceProvider = services.BuildServiceProvider();

        await DatabaseMigrationExtensions.SeedBootstrapAdminAsync(serviceProvider);

        var admin = await dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == "ADMIN@ELIXOM.COM");
        Assert.NotNull(admin);
        Assert.Equal("admin@elixom.com", admin.Email);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task SeedBootstrapAdminAsync_PromotesExistingUserToAdmin_WhenUserExists()
    {
        using var dbContext = CreateInMemoryDbContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@elixom.com",
            NormalizedEmail = "ADMIN@ELIXOM.COM",
            FullName = "Standard User",
            Role = UserRole.User,
            IsActive = false
        };
        await dbContext.Users.AddAsync(existingUser);
        await dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton(Options.Create(new AuthenticationOptions
        {
            DefaultAdminEmail = "admin@elixom.com"
        }));
        var serviceProvider = services.BuildServiceProvider();

        await DatabaseMigrationExtensions.SeedBootstrapAdminAsync(serviceProvider);

        var admin = await dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == "ADMIN@ELIXOM.COM");
        Assert.NotNull(admin);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.True(admin.IsActive);
    }
}
