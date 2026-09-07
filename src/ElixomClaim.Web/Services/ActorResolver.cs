using System.Security.Claims;
using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Services;

public record ActorContext(
    User User,
    string ClientId,
    string Scope,
    string CorrelationId,
    string RemoteIpAddress,
    bool IsMcp
);

public interface IActorResolver
{
    Task<Result<ActorContext>> ResolveActorAsync(
        HttpContext httpContext,
        string requiredScope,
        bool isMcp,
        CancellationToken cancellationToken = default);

    Task LogAuditAsync(
        ActorContext actor,
        string action,
        string target,
        object? beforeState = null,
        object? afterState = null,
        CancellationToken cancellationToken = default);
}

public class ActorResolver : IActorResolver
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ActorResolver> _logger;

    public ActorResolver(
        ApplicationDbContext dbContext,
        IAuditService auditService,
        ILogger<ActorResolver> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<ActorContext>> ResolveActorAsync(
        HttpContext httpContext,
        string requiredScope,
        bool isMcp,
        CancellationToken cancellationToken = default)
    {
        if (httpContext == null || httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Result<ActorContext>.Failure("Unauthenticated request.");
        }

        var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userIdGuid))
        {
            return Result<ActorContext>.Failure("Invalid or missing user identity.");
        }

        var scopeClaim = httpContext.User.FindFirstValue("scope") ?? string.Empty;
        var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ActorContext>.Failure($"Missing required OAuth scope '{requiredScope}'.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdGuid, cancellationToken);
        if (user == null)
        {
            return Result<ActorContext>.Failure("User account not found.");
        }

        if (!user.IsActive)
        {
            return Result<ActorContext>.Failure("User account is inactive.");
        }

        if (user.Role == UserRole.Blocked)
        {
            return Result<ActorContext>.Failure("User account is blocked.");
        }

        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        if (!httpContext.Response.Headers.ContainsKey("X-Correlation-ID"))
        {
            httpContext.Response.Headers["X-Correlation-ID"] = correlationId;
        }

        var clientId = httpContext.User.FindFirstValue("client_id") ?? "unknown";
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var context = new ActorContext(user, clientId, scopeClaim, correlationId, remoteIp, isMcp);
        return Result<ActorContext>.Success(context);
    }

    public async Task LogAuditAsync(
        ActorContext actor,
        string action,
        string target,
        object? beforeState = null,
        object? afterState = null,
        CancellationToken cancellationToken = default)
    {
        await _auditService.LogAsync(
            action: action,
            target: target,
            actorUserId: actor.User.Id.ToString(),
            actorEmail: actor.User.Email,
            correlationId: actor.CorrelationId,
            isMcpOperation: actor.IsMcp,
            beforeState: beforeState,
            afterState: afterState,
            cancellationToken: cancellationToken);
    }
}
