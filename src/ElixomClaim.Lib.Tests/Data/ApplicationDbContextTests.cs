using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class ApplicationDbContextTests
{
    [Fact]
    public void DefaultSchema_IsDbClaim()
    {
        Assert.Equal("dbclaim", ApplicationDbContext.DefaultSchema);
    }

    [Fact]
    public void DbContextOptions_CanBeConfigured()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "Test_Db")
            .Options;

        using var context = new ApplicationDbContext(options);
        Assert.NotNull(context);
        Assert.Equal("dbclaim", ApplicationDbContext.DefaultSchema);
    }

    [Fact]
    public void DependencyInjection_RegistersServicesAndDbContext()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddClaimLibraryServices(configuration);
        var provider = services.BuildServiceProvider();

        var clock = provider.GetService<ISystemClock>();
        Assert.NotNull(clock);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);

        var dbContext = provider.GetService<ApplicationDbContext>();
        Assert.NotNull(dbContext);
    }
}
