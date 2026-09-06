namespace ElixomClaim.Lib.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;

    public bool IsActive { get; set; } = true;

    public string? BankAccountName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? BankBranchCode { get; set; }

    public string? GetMaskedBankAccountNumber()
    {
        if (string.IsNullOrWhiteSpace(BankAccountNumber))
        {
            return null;
        }

        var trimmed = BankAccountNumber.Trim();
        if (trimmed.Length <= 4)
        {
            return "****";
        }

        return new string('*', trimmed.Length - 4) + trimmed[^4..];
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record UserProfileSummary(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankName,
    string? BankBranchCode
)
{
    public static UserProfileSummary FromUser(User user, bool includeFullBankDetails = false)
    {
        return new UserProfileSummary(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.IsActive,
            user.BankAccountName,
            includeFullBankDetails ? user.BankAccountNumber : user.GetMaskedBankAccountNumber(),
            user.BankName,
            user.BankBranchCode
        );
    }
}
