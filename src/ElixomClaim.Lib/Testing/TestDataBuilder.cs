namespace ElixomClaim.Lib.Testing;

public class TestUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "user@example.com";
    public string FullName { get; set; } = "Sample User";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public string BankAccountNumber { get; set; } = "***REDACTED***";
    public string BankBranchCode { get; set; } = "00000";
}

public class TestDataBuilder
{
    public static TestUser CreateUser(
        string? email = null,
        string role = "User",
        string fullName = "Anonymized Test User")
    {
        var id = Guid.NewGuid();
        return new TestUser
        {
            Id = id,
            Email = email ?? $"testuser_{id.ToString("N")[..8]}@anonymized.example.com",
            FullName = fullName,
            Role = role,
            IsActive = true,
            BankAccountNumber = "9999****1234",
            BankBranchCode = "00123"
        };
    }

    public static TestUser CreateAdmin(string? email = null)
    {
        return CreateUser(email ?? "admin@anonymized.example.com", role: "Administrator", fullName: "Anonymized System Admin");
    }

    public static TestUser CreateAccountant(string? email = null)
    {
        return CreateUser(email ?? "accountant@anonymized.example.com", role: "Accountant", fullName: "Anonymized Accountant");
    }
}
