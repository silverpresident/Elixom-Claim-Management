namespace ElixomClaim.Lib.Entities;

public class OAuthClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    public string RedirectUrisJson { get; set; } = "[]";
    public string AllowedGrantTypes { get; set; } = "authorization_code,refresh_token";
    public string AllowedScopes { get; set; } = "openid,profile,email,mcp:access";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OAuthAuthorizationCode
{
    public string CodeHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = "S256";
    public bool IsUsed { get; set; } = false;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OAuthToken
{
    public string TokenId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenType { get; set; } = "AccessToken"; // AccessToken or RefreshToken
    public string ClientId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? RefreshTokenFamilyId { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OAuthConsent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}
