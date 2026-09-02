using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public record OAuthClientRegistrationResult(string ClientId, string ClientSecret, string ClientName, List<string> RedirectUris);
public record OAuthTokenResult(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, string Scope);
public record OAuthTokenValidationResult(bool IsValid, User? User, string? Scope, string? ClientId, string? Error);

public interface IOAuthService
{
    Task<OAuthClientRegistrationResult> RegisterClientAsync(string clientName, IEnumerable<string> redirectUris, CancellationToken cancellationToken = default);
    Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);
    Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default);
    Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default);

    Task<string> CreateAuthorizationCodeAsync(
        string clientId,
        string userId,
        string redirectUri,
        string scope,
        string codeChallenge,
        string codeChallengeMethod = "S256",
        CancellationToken cancellationToken = default);

    Task<OAuthTokenResult?> ExchangeCodeForTokensAsync(
        string code,
        string clientId,
        string? clientSecret,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken = default);

    Task<OAuthTokenResult?> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        string? clientSecret,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<OAuthTokenValidationResult> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}
