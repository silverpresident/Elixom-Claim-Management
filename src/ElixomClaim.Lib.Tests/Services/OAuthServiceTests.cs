using System.Security.Cryptography;
using System.Text;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
