using System.Security.Claims;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        public Task<bool> ValidateClientAuthenticationAsync(string clientId, string? clientSecret, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ValidateRequestedScopesAsync(string clientId, string requestedScope, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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

    [Fact]
    public async Task BearerTransport_RejectsRevokedAndExpiredOAuthAccessTokens()
    {
        using var host = await CreateProtectedHostAsync(Guid.NewGuid().ToString("N"));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var oauth = scope.ServiceProvider.GetRequiredService<IOAuthService>();
        var user = new User { Id = Guid.NewGuid(), Email = "transport-user@example.test", NormalizedEmail = "TRANSPORT-USER@EXAMPLE.TEST", FullName = "Transport User", Role = UserRole.User, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var revokedToken = await IssueAccessTokenAsync(oauth, user.Id);
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", revokedToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/transport-probe")).StatusCode);

        await oauth.RevokeTokenAsync(revokedToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/transport-probe")).StatusCode);

        var expiredToken = await IssueAccessTokenAsync(oauth, user.Id);
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expiredToken))).ToLowerInvariant();
        var storedToken = await db.OAuthTokens.SingleAsync(token => token.TokenHash == tokenHash);
        storedToken.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/transport-probe")).StatusCode);
    }

    private static async Task<string> IssueAccessTokenAsync(IOAuthService oauth, Guid userId)
    {
        const string verifier = "transport_verifier_1234567890_abcdefghijklmnopqrstuvwxyz";
        const string redirectUri = "https://transport.example.test/callback";
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var registration = await oauth.RegisterClientAsync("Transport test client", [redirectUri]);
        var code = await oauth.CreateAuthorizationCodeAsync(registration.ClientId, userId.ToString(), redirectUri, "mcp:access", challenge);
        var tokens = await oauth.ExchangeCodeForTokensAsync(code, registration.ClientId, registration.ClientSecret, redirectUri, verifier);
        return Assert.IsType<OAuthTokenResult>(tokens).AccessToken;
    }

    private static async Task<IHost> CreateProtectedHostAsync(string databaseName) => await new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer().ConfigureServices(services =>
    {
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddRouting();
        services.AddAuthentication(BearerTokenAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(BearerTokenAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization(options => options.AddPolicy("McpAccess", policy => { policy.AddAuthenticationSchemes(BearerTokenAuthenticationHandler.SchemeName); policy.RequireAuthenticatedUser(); policy.RequireClaim("scope", "mcp:access"); }));
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapGet("/transport-probe", () => Results.Ok()).RequireAuthorization("McpAccess")); })).StartAsync();
}
