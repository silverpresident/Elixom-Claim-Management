using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace ElixomClaim.Web.Tests.Authentication;

public class ActorResolverTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private class FakeAuditService : IAuditService
    {
        public string? LastAction { get; private set; }
        public string? LastTarget { get; private set; }
        public string? LastActorUserId { get; private set; }
        public string? LastActorEmail { get; private set; }
        public string? LastCorrelationId { get; private set; }
        public bool? LastIsMcpOperation { get; private set; }

        public Task LogAsync(
            string action,
            AuditEntity entity,
            object? beforeState = null,
            object? afterState = null,
            string? actorUserId = null,
            string? actorEmail = null,
            string? correlationId = null,
            string? ipAddress = null,
            bool isMcpOperation = false,
            CancellationToken cancellationToken = default) =>
            LogAsync(action, $"{entity.EntityType}:{entity.EntityId}", beforeState, afterState, actorUserId, actorEmail, correlationId, ipAddress, isMcpOperation, cancellationToken);

        public Task LogAsync(
            string action,
            string target,
            object? beforeState = null,
            object? afterState = null,
            string? actorUserId = null,
            string? actorEmail = null,
            string? correlationId = null,
            string? ipAddress = null,
            bool isMcpOperation = false,
            CancellationToken cancellationToken = default)
        {
            LastAction = action;
            LastTarget = target;
            LastActorUserId = actorUserId;
            LastActorEmail = actorEmail;
            LastCorrelationId = correlationId;
            LastIsMcpOperation = isMcpOperation;
            return Task.CompletedTask;
        }

        public string RedactJson(string json) => json;
    }

    [Fact]
    public async Task ResolveActorAsync_ReturnsFailure_WhenUnauthenticated()
    {
        using var db = CreateDbContext();
        var audit = new FakeAuditService();
        var resolver = new ActorResolver(db, audit, NullLogger<ActorResolver>.Instance);

        var httpContext = new DefaultHttpContext();
        var result = await resolver.ResolveActorAsync(httpContext, "api:access", isMcp: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unauthenticated request.", result.Error);
    }

    [Fact]
    public async Task ResolveActorAsync_ReturnsFailure_WhenMissingRequiredScope()
    {
        using var db = CreateDbContext();
        var audit = new FakeAuditService();
        var resolver = new ActorResolver(db, audit, NullLogger<ActorResolver>.Instance);

        var user = new User { Id = Guid.NewGuid(), Email = "user@example.test", FullName = "Test User", Role = UserRole.User, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new SecurityClaim("scope", "openid profile email mcp:access") // missing api:access
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var result = await resolver.ResolveActorAsync(httpContext, "api:access", isMcp: false);

        Assert.False(result.IsSuccess);
        Assert.Contains("Missing required OAuth scope 'api:access'", result.Error);
    }

    [Fact]
    public async Task ResolveActorAsync_ReturnsFailure_WhenUserIsBlockedOrInactive()
    {
        using var db = CreateDbContext();
        var audit = new FakeAuditService();
        var resolver = new ActorResolver(db, audit, NullLogger<ActorResolver>.Instance);

        var blockedUser = new User { Id = Guid.NewGuid(), Email = "blocked@example.test", FullName = "Blocked User", Role = UserRole.Blocked, IsActive = true };
        db.Users.Add(blockedUser);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, blockedUser.Id.ToString()),
            new SecurityClaim("scope", "api:access")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var result = await resolver.ResolveActorAsync(httpContext, "api:access", isMcp: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("User account is blocked.", result.Error);
    }

    [Fact]
    public async Task ResolveActorAsync_ReturnsSuccess_AndSetsHeaders_WhenValid()
    {
        using var db = CreateDbContext();
        var audit = new FakeAuditService();
        var resolver = new ActorResolver(db, audit, NullLogger<ActorResolver>.Instance);

        var user = new User { Id = Guid.NewGuid(), Email = "manager@example.test", FullName = "Manager User", Role = UserRole.Manager, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "test-correlation-123";
        var claims = new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new SecurityClaim("scope", "openid profile email mcp:access"),
            new SecurityClaim("client_id", "client_xyz")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var result = await resolver.ResolveActorAsync(httpContext, "mcp:access", isMcp: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal("client_xyz", result.Value.ClientId);
        Assert.Equal("test-correlation-123", result.Value.CorrelationId);
        Assert.True(result.Value.IsMcp);
        Assert.Equal("test-correlation-123", httpContext.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Fact]
    public async Task LogAuditAsync_PassesIsMcpFlagAndActorDetailsToAuditService()
    {
        using var db = CreateDbContext();
        var audit = new FakeAuditService();
        var resolver = new ActorResolver(db, audit, NullLogger<ActorResolver>.Instance);

        var user = new User { Id = Guid.NewGuid(), Email = "actor@example.test", FullName = "Actor User", Role = UserRole.Accountant, IsActive = true };
        var actorContext = new ActorContext(user, "client_1", "mcp:access", "corr_123", "127.0.0.1", IsMcp: true);

        await resolver.LogAuditAsync(actorContext, "TEST_ACTION", new AuditEntity("Target", "123"));

        Assert.Equal("TEST_ACTION", audit.LastAction);
        Assert.Equal("Target:123", audit.LastTarget);
        Assert.Equal(user.Id.ToString(), audit.LastActorUserId);
        Assert.Equal(user.Email, audit.LastActorEmail);
        Assert.Equal("corr_123", audit.LastCorrelationId);
        Assert.True(audit.LastIsMcpOperation);
    }
}
