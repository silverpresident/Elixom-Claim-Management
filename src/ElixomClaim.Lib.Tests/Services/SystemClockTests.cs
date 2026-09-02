using ElixomClaim.Lib.Services;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsDateTimeInUtcKind()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.True(now <= DateTime.UtcNow);
    }
}
