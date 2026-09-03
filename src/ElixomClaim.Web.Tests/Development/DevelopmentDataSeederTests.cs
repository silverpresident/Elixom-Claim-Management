using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElixomClaim.Web.Tests.Development;

public class DevelopmentDataSeederTests
{
    [Fact]
    public async Task InitializeAsync_SeedsRepresentativeDataAcrossImplementedAreas()
    {
        var services = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        await using var provider = services.BuildServiceProvider();

        await DevelopmentDataSeeder.InitializeAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.False((await db.Users.SingleAsync(user => user.Role == UserRole.Blocked)).IsActive);
        Assert.NotEmpty(await db.Claims.ToListAsync());
        Assert.NotEmpty(await db.CollectionClients.ToListAsync());
        Assert.NotEmpty(await db.CollectionTransactions.ToListAsync());
        Assert.NotEmpty(await db.SalaryDefinitions.ToListAsync());
        Assert.NotEmpty(await db.Payrolls.ToListAsync());
        Assert.NotEmpty(await db.JobPayments.ToListAsync());
        Assert.NotEmpty(await db.EmailOutboxItems.ToListAsync());
        Assert.NotEmpty(await db.EmailLogs.ToListAsync());
        Assert.NotEmpty(await db.AuditRecords.ToListAsync());
        Assert.NotEmpty(await db.OAuthClients.ToListAsync());
    }
}
