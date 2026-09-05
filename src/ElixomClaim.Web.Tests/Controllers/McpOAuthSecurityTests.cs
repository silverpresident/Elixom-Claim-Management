using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Controllers;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class McpOAuthSecurityTests
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
    public async Task DynamicClientRegistration_ValidatesMetadataAndRejectsInvalid()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);
        var controller = new OAuthController(oauth, NullLogger<OAuthController>.Instance);

        // Valid registration
        var req = new OAuthController.ClientRegistrationRequest("TestClient", new List<string> { "https://test.com/cb" });
        var res = await controller.Register(req);
        var created = Assert.IsType<CreatedResult>(res);
        Assert.NotNull(created.Value);

        // Invalid registration missing client name
        var invalidReq = new OAuthController.ClientRegistrationRequest("", new List<string> { "https://test.com/cb" });
        var invalidRes = await controller.Register(invalidReq);
        Assert.IsType<BadRequestObjectResult>(invalidRes);
    }

    [Fact]
    public async Task AuthorizeEndpoint_RejectsInvalidRedirectUri_And_NonPKCE()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);
        var controller = new OAuthController(oauth, NullLogger<OAuthController>.Instance);

        var reg = await oauth.RegisterClientAsync("Client", new[] { "https://app.com/callback" });

        // Invalid redirect URI
        var badRedirectRes = await controller.Authorize("code", reg.ClientId, "https://attacker.com/callback", "mcp:access", "state123", "challenge123", "S256");
        Assert.IsType<BadRequestObjectResult>(badRedirectRes);

        // Missing PKCE S256 challenge
        var noPkceRes = await controller.Authorize("code", reg.ClientId, "https://app.com/callback", "mcp:access", "state123", "", "plain");
        Assert.IsType<BadRequestObjectResult>(noPkceRes);
    }

    [Fact]
    public async Task AuthorizeConsent_RevalidatesParameters_AndPersistsConsent()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);
        var controller = new OAuthController(oauth, NullLogger<OAuthController>.Instance);

        var reg = await oauth.RegisterClientAsync("Client", new[] { "https://app.com/callback" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FullName = "User Test",
            Role = UserRole.User,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Email, user.Email)
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Invalid client on consent POST
        var badClientRes = await controller.AuthorizeConsent("nonexistent", "https://app.com/callback", "mcp:access", "s", "c", "S256", "Approve");
        Assert.IsType<BadRequestObjectResult>(badClientRes);

        // Invalid redirect URI on consent POST
        var badRedirectRes = await controller.AuthorizeConsent(reg.ClientId, "https://attacker.com/callback", "mcp:access", "s", "c", "S256", "Approve");
        Assert.IsType<BadRequestObjectResult>(badRedirectRes);

        // Valid consent POST -> redirects with authorization code and records consent
        var validRes = await controller.AuthorizeConsent(reg.ClientId, "https://app.com/callback", "mcp:access", "state123", "challenge123", "S256", "Approve");
        var redirectRes = Assert.IsType<RedirectResult>(validRes);
        Assert.Contains("code=", redirectRes.Url);
        Assert.Contains("state=state123", redirectRes.Url);

        Assert.True(await oauth.HasConsentAsync(user.Id.ToString(), reg.ClientId, "mcp:access"));

        // Subsequent GET /oauth/authorize should auto-bypass consent view because consent is recorded
        var getAuthRes = await controller.Authorize("code", reg.ClientId, "https://app.com/callback", "mcp:access", "state123", "challenge123", "S256");
        var autoRedirectRes = Assert.IsType<RedirectResult>(getAuthRes);
        Assert.Contains("code=", autoRedirectRes.Url);
    }

    [Fact]
    public async Task RefreshTokenReplay_RevokesEntireTokenFamily()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var oauth = new OAuthService(db, audit, NullLogger<OAuthService>.Instance);

        var client = await oauth.RegisterClientAsync("Client", new[] { "https://app.com/cb" });
        var verifier = "secret_code_verifier_1234567890_abcdef";
        var challenge = CalculateS256Challenge(verifier);

        var code = await oauth.CreateAuthorizationCodeAsync(client.ClientId, Guid.NewGuid().ToString(), "https://app.com/cb", "mcp:access", challenge);
        var tokens = await oauth.ExchangeCodeForTokensAsync(code, client.ClientId, client.ClientSecret, "https://app.com/cb", verifier);
        Assert.NotNull(tokens);

        // First refresh: rotates refresh token
        var refreshedTokens = await oauth.RefreshTokenAsync(tokens.RefreshToken, client.ClientId, client.ClientSecret);
        Assert.NotNull(refreshedTokens);

        // Replay old refresh token: MUST fail and trigger family revocation
        var replayedTokens = await oauth.RefreshTokenAsync(tokens.RefreshToken, client.ClientId, client.ClientSecret);
        Assert.Null(replayedTokens);

        // Validate that new refresh token is also revoked due to family revocation
        var reattemptNewRefresh = await oauth.RefreshTokenAsync(refreshedTokens.RefreshToken, client.ClientId, client.ClientSecret);
        Assert.Null(reattemptNewRefresh);
    }

    [Fact]
    public async Task McpController_RejectsRequestsWithoutMcpScope()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);
        var tools = new ClaimTools(claimService, audit);
        var controller = new McpClaimsController(tools, db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            NormalizedEmail = "USER@TEST.COM",
            FullName = "Test User",
            Role = UserRole.User,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // HttpContext without mcp:access scope claim
        var httpContext = new DefaultHttpContext();
        var identity = new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim("scope", "openid profile email") // Missing mcp:access
        }, BearerTokenAuthenticationHandler.SchemeName);
        httpContext.User = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.List(new ListClaimsRequest());
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task McpClaimTools_CrossUserIsolation_UserCannotReadOthersData()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var claimService = new ClaimService(db, audit, NullLogger<ClaimService>.Instance);
        var tools = new ClaimTools(claimService, audit);

        var userA = new User { Id = Guid.NewGuid(), Email = "a@test.com", NormalizedEmail = "A@TEST.COM", FullName = "User A", Role = UserRole.User, IsActive = true };
        var userB = new User { Id = Guid.NewGuid(), Email = "b@test.com", NormalizedEmail = "B@TEST.COM", FullName = "User B", Role = UserRole.User, IsActive = true };
        db.Users.AddRange(userA, userB);

        var claimB = new ElixomClaim.Lib.Entities.Claim { ClaimantUserId = userB.Id, Title = "Secret Claim B", Description = "Desc", Amount = 100m, Status = ClaimStatus.Draft };
        db.Claims.Add(claimB);
        await db.SaveChangesAsync();

        // User A attempts to list claims via MCP -> should only see own claims
        var listRes = await tools.ListClaimsAsync(userA, new ListClaimsRequest(), CancellationToken.None);
        Assert.True(listRes.Success);
        Assert.Empty(listRes.Claims!);

        // User A attempts to get User B's claim by ID -> should return access denied / not found
        var getRes = await tools.GetClaimAsync(userA, new GetClaimRequest(claimB.Id), CancellationToken.None);
        Assert.False(getRes.Success);
        Assert.Contains("access denied", getRes.Error, StringComparison.OrdinalIgnoreCase);
    }
}
