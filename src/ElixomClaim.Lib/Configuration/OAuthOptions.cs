using System.ComponentModel.DataAnnotations;

namespace ElixomClaim.Lib.Configuration;

public class OAuthOptions
{
    public const string SectionName = "OAuth";

    [Required(ErrorMessage = "OAuth Issuer is required.")]
    public string Issuer { get; set; } = "ElixomClaim.OAuth";

    [Range(60, 86400, ErrorMessage = "AccessTokenLifetimeSeconds must be between 60 and 86400 seconds.")]
    public int AccessTokenLifetimeSeconds { get; set; } = 3600;

    [Range(3600, 2592000, ErrorMessage = "RefreshTokenLifetimeSeconds must be between 3600 and 2592000 seconds.")]
    public int RefreshTokenLifetimeSeconds { get; set; } = 1209600; // 14 days

    public string ToRedactedString()
    {
        return $"Issuer: '{Issuer}', AccessTokenLifetimeSeconds: {AccessTokenLifetimeSeconds}, RefreshTokenLifetimeSeconds: {RefreshTokenLifetimeSeconds}";
    }
}
