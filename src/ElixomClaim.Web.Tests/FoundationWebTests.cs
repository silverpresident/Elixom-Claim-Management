using ElixomClaim.Lib.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElixomClaim.Web.Tests;

public class FoundationWebTests
{
    [Fact]
    public void SystemClock_CanBeResolvedFromServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock, SystemClock>();
        var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<ISystemClock>();
        Assert.NotNull(clock);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}
