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
        Assert.Null(user.BankAccountName);
        Assert.Null(user.BankAccountNumber);
        Assert.Null(user.BankName);
        Assert.Null(user.BankBranchCode);
        Assert.True(user.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(user.UpdatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("1234567890", "******7890")]
    [InlineData("9876", "****")]
    [InlineData("123", "****")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void GetMaskedBankAccountNumber_MasksCorrectly(string? inputAccountNumber, string? expectedMasked)
    {
        var user = new User { BankAccountNumber = inputAccountNumber };
        Assert.Equal(expectedMasked, user.GetMaskedBankAccountNumber());
    }

    [Fact]
    public void UserProfileSummary_FromUser_RedactsAccountNumberWhenUnprivileged()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "john@example.com",
            FullName = "John Swimmer",
            Role = UserRole.User,
            BankAccountName = "John Swimmer",
            BankAccountNumber = "1234567890",
            BankName = "National Commercial Bank",
            BankBranchCode = "001"
        };

        var redactedSummary = UserProfileSummary.FromUser(user, includeFullBankDetails: false);
        Assert.Equal("******7890", redactedSummary.BankAccountNumber);
        Assert.Equal("National Commercial Bank", redactedSummary.BankName);
        Assert.Equal("John Swimmer", redactedSummary.BankAccountName);

        var fullSummary = UserProfileSummary.FromUser(user, includeFullBankDetails: true);
        Assert.Equal("1234567890", fullSummary.BankAccountNumber);
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
