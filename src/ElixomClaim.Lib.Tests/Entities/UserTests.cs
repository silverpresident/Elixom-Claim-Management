using ElixomClaim.Lib.Entities;
using Xunit;

namespace ElixomClaim.Lib.Tests.Entities;

public class UserTests
{
    [Fact]
    public void User_Initialization_SetsDefaultValues()
    {
        var user = new User
        {
            Email = "jane.doe@example.com",
            NormalizedEmail = "JANE.DOE@EXAMPLE.COM",
            FullName = "Jane Doe"
        };

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("jane.doe@example.com", user.Email);
        Assert.Equal("JANE.DOE@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Jane Doe", user.FullName);
        Assert.Equal(UserRole.User, user.Role);
        Assert.True(user.IsActive);
        Assert.Null(user.BankAccountNumber);
        Assert.Null(user.BankBranchCode);
        Assert.True(user.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(user.UpdatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(UserRole.Blocked, 0)]
    [InlineData(UserRole.User, 1)]
    [InlineData(UserRole.Teller, 2)]
    [InlineData(UserRole.Manager, 3)]
    [InlineData(UserRole.Accountant, 4)]
    [InlineData(UserRole.Administrator, 5)]
    public void UserRole_HierarchicalValue_IsPreserved(UserRole role, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)role);
    }
}
