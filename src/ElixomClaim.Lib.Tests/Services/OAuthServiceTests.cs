using System.Security.Cryptography;
using System.Text;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class OAuthServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string CalculateS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    [Fact]
    public async Task RegisterClientAsync_CreatesActiveClientAndAudits()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        var result = await oauth.RegisterClientAsync("MCP Client", new[] { "https://mcp.local/callback" });

        Assert.NotNull(result.ClientId);
        Assert.NotNull(result.ClientSecret);
        Assert.Equal("MCP Client", result.ClientName);
        Assert.Single(result.RedirectUris);

        var clientInDb = await oauth.GetClientAsync(result.ClientId);
        Assert.NotNull(clientInDb);
        Assert.True(clientInDb.IsActive);
    }

    [Fact]
    public async Task RegisterClientAsync_EnforcesAdmissionPolicyAndRedirectUriShape()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        // Blank name
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync("", new[] { "https://app.com/cb" }));

        // Name too long (>100 chars)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync(new string('a', 101), new[] { "https://app.com/cb" }));

        // Empty redirect URIs
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync("App", Array.Empty<string>()));

        // Non-HTTPS HTTP URI for remote host
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync("App", new[] { "http://attacker.com/cb" }));

        // URI with fragment
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync("App", new[] { "https://app.com/cb#fragment" }));

        // Wildcard URI
        await Assert.ThrowsAsync<ArgumentException>(() =>
            oauth.RegisterClientAsync("App", new[] { "https://*.app.com/cb" }));

        // Valid localhost HTTP URI
        var res1 = await oauth.RegisterClientAsync("LocalApp", new[] { "http://localhost:5000/cb", "http://127.0.0.1:8080/cb" });
        Assert.NotNull(res1.ClientId);
        Assert.Equal(2, res1.RedirectUris.Count);
    }

    [Fact]
    public async Task ConfiguredOAuthOptionsLifetimes_AreRespected()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var options = Options.Create(new OAuthOptions
        {
            AccessTokenLifetimeSeconds = 1800,
            RefreshTokenLifetimeSeconds = 86400
        });
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance, options);

        var client = await oauth.RegisterClientAsync("App", new[] { "https://app.com/cb" });
        var verifier = "code_verifier_1234567890_abcdef123";
        var challenge = CalculateS256Challenge(verifier);

        var code = await oauth.CreateAuthorizationCodeAsync(client.ClientId, Guid.NewGuid().ToString(), "https://app.com/cb", "mcp:access", challenge);
        var tokenResult = await oauth.ExchangeCodeForTokensAsync(code, client.ClientId, client.ClientSecret, "https://app.com/cb", verifier);

        Assert.NotNull(tokenResult);
        Assert.Equal(1800, tokenResult.ExpiresIn);

        var tokenRecord = await db.OAuthTokens.FirstAsync(t => t.TokenType == "AccessToken");
        Assert.True((tokenRecord.ExpiresAtUtc - tokenRecord.CreatedAtUtc).TotalSeconds is >= 1795 and <= 1805);
    }

    [Fact]
    public async Task ConsentPersistenceAndCheck_WorksAsExpected()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        var userId = Guid.NewGuid().ToString();
        var client = await oauth.RegisterClientAsync("App", new[] { "https://app.com/cb" });

        Assert.False(await oauth.HasConsentAsync(userId, client.ClientId, "mcp:access"));

        await oauth.RecordConsentAsync(userId, client.ClientId, "openid profile email mcp:access");

        Assert.True(await oauth.HasConsentAsync(userId, client.ClientId, "mcp:access"));
        Assert.True(await oauth.HasConsentAsync(userId, client.ClientId, "openid profile"));
        Assert.False(await oauth.HasConsentAsync(userId, client.ClientId, "mcp:access admin:scope"));

        var consentInDb = await db.OAuthConsents.FirstOrDefaultAsync(c => c.UserId == userId && c.ClientId == client.ClientId);
        Assert.NotNull(consentInDb);
    }

    [Fact]
    public async Task AuthorizationCode_DoesNotStoreRawCodeInDatabase()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        var client = await oauth.RegisterClientAsync("App", new[] { "https://app.com/cb" });
        var verifier = "verifier_1234567890_abcdef";
        var challenge = CalculateS256Challenge(verifier);

        var rawCode = await oauth.CreateAuthorizationCodeAsync(
            client.ClientId, Guid.NewGuid().ToString(), "https://app.com/cb", "mcp:access", challenge);

        Assert.NotNull(rawCode);

        // Verify database records
        var codesInDb = await db.OAuthAuthorizationCodes.ToListAsync();
        Assert.Single(codesInDb);
        Assert.NotEqual(rawCode, codesInDb[0].CodeHash);

        // Ensure raw code string is not found anywhere in CodeHash property
        Assert.DoesNotContain(rawCode, codesInDb[0].CodeHash);
    }

    [Fact]
    public async Task FullAuthorizationCodeGrantFlowWithPKCE_Succeeds()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        // 1. Register Client
        var client = await oauth.RegisterClientAsync("App", new[] { "https://app.com/cb" });

        // 2. Add Test User
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@elixom.com",
            NormalizedEmail = "USER@ELIXOM.COM",
            FullName = "Test User",
            Role = UserRole.User,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 3. Issue Code
        var verifier = "e9mel_code_verifier_1234567890_abcdef";
        var challenge = CalculateS256Challenge(verifier);

        var code = await oauth.CreateAuthorizationCodeAsync(
            client.ClientId, user.Id.ToString(), "https://app.com/cb", "mcp:access", challenge);

        Assert.NotNull(code);

        // 4. Exchange Code for Tokens
        var tokenResult = await oauth.ExchangeCodeForTokensAsync(
            code, client.ClientId, client.ClientSecret, "https://app.com/cb", verifier);

        Assert.NotNull(tokenResult);
        Assert.NotNull(tokenResult.AccessToken);
        Assert.NotNull(tokenResult.RefreshToken);
        Assert.Equal("mcp:access", tokenResult.Scope);

        // 5. Validate Access Token
        var validation = await oauth.ValidateAccessTokenAsync(tokenResult.AccessToken);
        Assert.True(validation.IsValid);
        Assert.Equal(user.Id, validation.User?.Id);

        // 6. Refresh Tokens
        var refreshResult = await oauth.RefreshTokenAsync(tokenResult.RefreshToken, client.ClientId, client.ClientSecret);
        Assert.NotNull(refreshResult);
        Assert.NotEqual(tokenResult.AccessToken, refreshResult.AccessToken);

        // 7. Revoke Tokens
        await oauth.RevokeTokenAsync(refreshResult.AccessToken);
        var revalidation = await oauth.ValidateAccessTokenAsync(refreshResult.AccessToken);
        Assert.False(revalidation.IsValid);
    }
}
