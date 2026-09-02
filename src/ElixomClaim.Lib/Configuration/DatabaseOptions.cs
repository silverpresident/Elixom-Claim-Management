using System.ComponentModel.DataAnnotations;

namespace ElixomClaim.Lib.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "ClaimDatabase connection string is required.")]
    public string ClaimDatabase { get; set; } = string.Empty;

    public string ToRedactedString()
    {
        if (string.IsNullOrWhiteSpace(ClaimDatabase))
        {
            return "ClaimDatabase: <Not Configured>";
        }

        // Redact Password / User ID / Pwd in connection string if present
        var parts = ClaimDatabase.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var redactedParts = parts.Select(part =>
        {
            var keyVal = part.Split('=', 2);
            if (keyVal.Length == 2)
            {
                var key = keyVal[0].Trim();
                if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Pwd", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("User ID", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("UID", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{key}=***REDACTED***";
                }
            }
            return part;
        });

        return $"ClaimDatabase: {string.Join(";", redactedParts)}";
    }
}
