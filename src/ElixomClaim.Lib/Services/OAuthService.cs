using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class OAuthService : IOAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(ApplicationDbContext dbContext, IAuditService auditService, ILogger<OAuthService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<OAuthClientRegistrationResult> RegisterClientAsync(string clientName, IEnumerable<string> redirectUris, CancellationToken cancellationToken = default)
    {
        var clientId = "client_" + RandomNumberGenerator.GetHexString(16);
        var clientSecret = "secret_" + RandomNumberGenerator.GetHexString(32);
        var clientSecretHash = HashString(clientSecret);
        var urisList = redirectUris.Distinct().ToList();

        var client = new OAuthClient
        {
            ClientId = clientId,
            ClientName = clientName,
            ClientSecretHash = clientSecretHash,
            RedirectUrisJson = JsonSerializer.Serialize(urisList),
            AllowedGrantTypes = "authorization_code,refresh_token",
            AllowedScopes = "openid profile email mcp:access",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OAuthClients.Add(client);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OAUTH_CLIENT_REGISTERED",
            target: $"OAuthClient:{clientId}",
            afterState: new { clientId, clientName, redirectUris = urisList },
            cancellationToken: cancellationToken);

        return new OAuthClientRegistrationResult(clientId, clientSecret, clientName, urisList);
    }

    public async Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);
    }

    public async Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientId, cancellationToken);
        if (client == null)
        {
            return false;
        }

        var secretHash = HashString(clientSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(client.ClientSecretHash),
            Encoding.UTF8.GetBytes(secretHash));
    }

    public async Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientId, cancellationToken);
        if (client == null)
        {
            return false;
        }

        var uris = JsonSerializer.Deserialize<List<string>>(client.RedirectUrisJson) ?? [];
        return uris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> CreateAuthorizationCodeAsync(
        string clientId,
        string userId,
        string redirectUri,
        string scope,
        string codeChallenge,
        string codeChallengeMethod = "S256",
        CancellationToken cancellationToken = default)
    {
        var code = "code_" + RandomNumberGenerator.GetHexString(32);
        var codeHash = HashString(code);

        var authCode = new OAuthAuthorizationCode
        {
            Code = code,
            CodeHash = codeHash,
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            Scope = scope,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            IsUsed = false,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OAuthAuthorizationCodes.Add(authCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OAUTH_AUTH_CODE_ISSUED",
            target: $"OAuthClient:{clientId}",
            actorUserId: userId,
            afterState: new { clientId, scope, redirectUri },
            cancellationToken: cancellationToken);

        return code;
    }

    public async Task<OAuthTokenResult?> ExchangeCodeForTokensAsync(
        string code,
        string clientId,
        string? clientSecret,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var codeHash = HashString(code);
        var authCode = await _dbContext.OAuthAuthorizationCodes.FirstOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);

        if (authCode == null || authCode.IsUsed || authCode.ExpiresAtUtc < DateTime.UtcNow)
        {
            return null;
        }

        if (!string.Equals(authCode.ClientId, clientId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(authCode.RedirectUri, redirectUri, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(clientSecret))
        {
            var isValidSecret = await ValidateClientSecretAsync(clientId, clientSecret, cancellationToken);
            if (!isValidSecret)
            {
                return null;
            }
        }

        // Verify PKCE S256
        if (!VerifyPkce(codeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
        {
            return null;
        }

        authCode.IsUsed = true;

        var accessToken = "at_" + RandomNumberGenerator.GetHexString(32);
        var accessTokenHash = HashString(accessToken);
        var refreshToken = "rt_" + RandomNumberGenerator.GetHexString(32);
        var refreshTokenHash = HashString(refreshToken);
        var familyId = "fam_" + RandomNumberGenerator.GetHexString(16);

        var now = DateTime.UtcNow;
        var accessExpires = now.AddHours(1);
        var refreshExpires = now.AddDays(14);

        var tokenRecord = new OAuthToken
        {
            TokenId = Guid.NewGuid().ToString("N"),
            TokenHash = accessTokenHash,
            TokenType = "AccessToken",
            ClientId = clientId,
            UserId = authCode.UserId,
            Scope = authCode.Scope,
            RefreshTokenFamilyId = familyId,
            IsRevoked = false,
            ExpiresAtUtc = accessExpires,
            CreatedAtUtc = now
        };

        var refreshTokenRecord = new OAuthToken
        {
            TokenId = Guid.NewGuid().ToString("N"),
            TokenHash = refreshTokenHash,
            TokenType = "RefreshToken",
            ClientId = clientId,
            UserId = authCode.UserId,
            Scope = authCode.Scope,
            RefreshTokenFamilyId = familyId,
            IsRevoked = false,
            ExpiresAtUtc = refreshExpires,
            CreatedAtUtc = now
        };

        _dbContext.OAuthTokens.Add(tokenRecord);
        _dbContext.OAuthTokens.Add(refreshTokenRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OAUTH_TOKENS_ISSUED",
            target: $"OAuthClient:{clientId}",
            actorUserId: authCode.UserId,
            afterState: new { clientId, scope = authCode.Scope, familyId },
            cancellationToken: cancellationToken);

        return new OAuthTokenResult(accessToken, "Bearer", 3600, refreshToken, authCode.Scope);
    }

    public async Task<OAuthTokenResult?> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        string? clientSecret,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashString(refreshToken);
        var tokenRecord = await _dbContext.OAuthTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.TokenType == "RefreshToken", cancellationToken);

        if (tokenRecord == null || tokenRecord.IsRevoked || tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
        {
            // If token was revoked/replayed, revoke entire family
            if (tokenRecord != null && tokenRecord.IsRevoked && !string.IsNullOrEmpty(tokenRecord.RefreshTokenFamilyId))
            {
                await RevokeFamilyAsync(tokenRecord.RefreshTokenFamilyId, cancellationToken);
            }
            return null;
        }

        if (!string.Equals(tokenRecord.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(clientSecret))
        {
            var isValidSecret = await ValidateClientSecretAsync(clientId, clientSecret, cancellationToken);
            if (!isValidSecret)
            {
                return null;
            }
        }

        // Revoke current refresh token (rotation)
        tokenRecord.IsRevoked = true;

        var newAccessToken = "at_" + RandomNumberGenerator.GetHexString(32);
        var newAccessTokenHash = HashString(newAccessToken);
        var newRefreshToken = "rt_" + RandomNumberGenerator.GetHexString(32);
        var newRefreshTokenHash = HashString(newRefreshToken);

        var now = DateTime.UtcNow;
        var accessExpires = now.AddHours(1);
        var refreshExpires = now.AddDays(14);

        var newAccessRecord = new OAuthToken
        {
            TokenId = Guid.NewGuid().ToString("N"),
            TokenHash = newAccessTokenHash,
            TokenType = "AccessToken",
            ClientId = clientId,
            UserId = tokenRecord.UserId,
            Scope = tokenRecord.Scope,
            RefreshTokenFamilyId = tokenRecord.RefreshTokenFamilyId,
            IsRevoked = false,
            ExpiresAtUtc = accessExpires,
            CreatedAtUtc = now
        };

        var newRefreshRecord = new OAuthToken
        {
            TokenId = Guid.NewGuid().ToString("N"),
            TokenHash = newRefreshTokenHash,
            TokenType = "RefreshToken",
            ClientId = clientId,
            UserId = tokenRecord.UserId,
            Scope = tokenRecord.Scope,
            RefreshTokenFamilyId = tokenRecord.RefreshTokenFamilyId,
            IsRevoked = false,
            ExpiresAtUtc = refreshExpires,
            CreatedAtUtc = now
        };

        _dbContext.OAuthTokens.Add(newAccessRecord);
        _dbContext.OAuthTokens.Add(newRefreshRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "OAUTH_TOKENS_REFRESHED",
            target: $"OAuthClient:{clientId}",
            actorUserId: tokenRecord.UserId,
            afterState: new { clientId, scope = tokenRecord.Scope, familyId = tokenRecord.RefreshTokenFamilyId },
            cancellationToken: cancellationToken);

        return new OAuthTokenResult(newAccessToken, "Bearer", 3600, newRefreshToken, tokenRecord.Scope);
    }

    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashString(token);
        var tokenRecord = await _dbContext.OAuthTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenRecord == null)
        {
            return true;
        }

        tokenRecord.IsRevoked = true;

        if (tokenRecord.TokenType == "RefreshToken" && !string.IsNullOrEmpty(tokenRecord.RefreshTokenFamilyId))
        {
            await RevokeFamilyAsync(tokenRecord.RefreshTokenFamilyId, cancellationToken);
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _auditService.LogAsync(
            action: "OAUTH_TOKEN_REVOKED",
            target: $"OAuthClient:{tokenRecord.ClientId}",
            actorUserId: tokenRecord.UserId,
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<OAuthTokenValidationResult> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashString(accessToken);
        var tokenRecord = await _dbContext.OAuthTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.TokenType == "AccessToken", cancellationToken);

        if (tokenRecord == null)
        {
            return new OAuthTokenValidationResult(false, null, null, null, "Token not found");
        }

        if (tokenRecord.IsRevoked)
        {
            return new OAuthTokenValidationResult(false, null, null, null, "Token has been revoked");
        }

        if (tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
        {
            return new OAuthTokenValidationResult(false, null, null, null, "Token has expired");
        }

        if (!Guid.TryParse(tokenRecord.UserId, out var userIdGuid))
        {
            return new OAuthTokenValidationResult(false, null, null, null, "Invalid user ID format");
        }
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdGuid, cancellationToken);
        if (user == null || !user.IsActive || user.Role == UserRole.Blocked)
        {
            return new OAuthTokenValidationResult(false, null, null, null, "User account is inactive or blocked");
        }

        return new OAuthTokenValidationResult(true, user, tokenRecord.Scope, tokenRecord.ClientId, null);
    }

    private async Task RevokeFamilyAsync(string familyId, CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.OAuthTokens
            .Where(t => t.RefreshTokenFamilyId == familyId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string HashString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool VerifyPkce(string codeVerifier, string codeChallenge, string method)
    {
        if (!string.Equals(method, "S256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var challengeCalculated = Base64UrlEncode(hash);
        return string.Equals(challengeCalculated, codeChallenge, StringComparison.Ordinal);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        var base64 = Convert.ToBase64String(input);
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
