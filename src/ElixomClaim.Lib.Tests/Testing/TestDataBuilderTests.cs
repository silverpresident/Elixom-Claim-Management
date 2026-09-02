using ElixomClaim.Lib.Testing;
using Xunit;

namespace ElixomClaim.Lib.Tests.Testing;

public class TestDataBuilderTests
{
    [Fact]
    public void CreateUser_ReturnsAnonymizedUserDefaults()
    {
        var user = TestDataBuilder.CreateUser(role: "Teller");

        Assert.NotNull(user);
        Assert.Equal("Teller", user.Role);
        Assert.Contains("anonymized.example.com", user.Email);
        Assert.True(user.IsActive);
        Assert.DoesNotContain("12345678", user.BankAccountNumber);
    }

    [Fact]
    public void CreateAdmin_ReturnsAdministratorUser()
    {
        var admin = TestDataBuilder.CreateAdmin("admin@test.com");

        Assert.Equal("Administrator", admin.Role);
        Assert.Equal("admin@test.com", admin.Email);
    }
}
