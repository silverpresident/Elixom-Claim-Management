using System.ComponentModel.DataAnnotations;

namespace ElixomClaim.Lib.Configuration;

public class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    [Required(ErrorMessage = "Google ClientId is required.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Google ClientSecret is required.")]
    public string ClientSecret { get; set; } = string.Empty;

    public string ToRedactedString()
    {
        var redactedSecret = string.IsNullOrWhiteSpace(ClientSecret) ? "<Not Configured>" : "***REDACTED***";
        return $"ClientId: '{ClientId}', ClientSecret: '{redactedSecret}'";
    }
}

public class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Required(ErrorMessage = "DefaultAdminEmail is required.")]
    [EmailAddress(ErrorMessage = "DefaultAdminEmail must be a valid email address.")]
    public string DefaultAdminEmail { get; set; } = string.Empty;

    public GoogleAuthOptions Google { get; set; } = new();

    public string ToRedactedString()
    {
        return $"DefaultAdminEmail: '{DefaultAdminEmail}', Google: [{Google.ToRedactedString()}]";
    }
}
