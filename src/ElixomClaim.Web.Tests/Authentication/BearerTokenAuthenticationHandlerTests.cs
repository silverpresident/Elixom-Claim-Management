using System.Security.Claims;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Web.Tests.Authentication;

public class BearerTokenAuthenticationHandlerTests
{
    private class FakeOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue => new();
        public AuthenticationSchemeOptions Get(string? name) => new();
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private class FakeOAuthService : IOAuthService
    {
        public OAuthTokenValidationResult ValidationResultToReturn { get; set; } =
            new OAuthTokenValidationResult(false, null, null, null, "Invalid token");

        public Task<OAuthTokenValidationResult> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ValidationResultToReturn);
        }

        public Task<OAuthClientRegistrationResult> RegisterClientAsync(string clientName, IEnumerable<string> redirectUris, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ValidateClientSecretAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordConsentAsync(string userId, string clientId, string scope, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> HasConsentAsync(string userId, string clientId, string requestedScope, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> CreateAuthorizationCodeAsync(string clientId, string userId, string redirectUri, string scope, string codeChallenge, string codeChallengeMethod = "S256", CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OAuthTokenResult?> ExchangeCodeForTokensAsync(string code, string clientId, string? clientSecret, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OAuthTokenResult?> RefreshTokenAsync(string refreshToken, string clientId, string? clientSecret, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsNoResult_WhenHeaderIsMissing()
    {
        var fakeOAuth = new FakeOAuthService();
        var handler = new BearerTokenAuthenticationHandler(
            new FakeOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default, fakeOAuth);

        var context = new DefaultHttpContext();
        await handler.InitializeAsync(new AuthenticationScheme(BearerTokenAuthenticationHandler.SchemeName, "Bearer", typeof(BearerTokenAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();
        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsSuccess_WhenTokenIsValid()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@elixom.com",
            FullName = "Test User",
            Role = UserRole.User,
            IsActive = true
        };

        var fakeOAuth = new FakeOAuthService
        {
            ValidationResultToReturn = new OAuthTokenValidationResult(true, user, "mcp:access", "client-123", null)
        };

        var handler = new BearerTokenAuthenticationHandler(
            new FakeOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default, fakeOAuth);

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        await handler.InitializeAsync(new AuthenticationScheme(BearerTokenAuthenticationHandler.SchemeName, "Bearer", typeof(BearerTokenAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Equal(user.Email, result.Principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(UserRole.User.ToString(), result.Principal.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsFail_WhenTokenIsExpiredOrRevoked()
    {
        var fakeOAuth = new FakeOAuthService
        {
            ValidationResultToReturn = new OAuthTokenValidationResult(false, null, null, null, "Token has expired")
        };

        var handler = new BearerTokenAuthenticationHandler(
            new FakeOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default, fakeOAuth);

        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer expired-token";

        await handler.InitializeAsync(new AuthenticationScheme(BearerTokenAuthenticationHandler.SchemeName, "Bearer", typeof(BearerTokenAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Token has expired", result.Failure?.Message);
    }
}
