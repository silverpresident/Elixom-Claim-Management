using ElixomClaim.Lib.Common;
using ElixomClaim.Web.Services;

namespace ElixomClaim.Web.Mcp.Tools;

/// <summary>
/// Resolves the concrete, authenticated OAuth actor for an MCP tool call.
/// Tool classes use this adapter rather than interpreting HTTP claims themselves.
/// </summary>
public sealed class McpToolActorAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IActorResolver _actorResolver;

    public McpToolActorAccessor(
        IHttpContextAccessor httpContextAccessor,
        IActorResolver actorResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _actorResolver = actorResolver;
    }

    public Task<Result<ActorContext>> ResolveAsync(CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext is null
            ? Task.FromResult(Result<ActorContext>.Failure("MCP request context is unavailable."))
            : _actorResolver.ResolveActorAsync(httpContext, "mcp:access", isMcp: true, cancellationToken);
    }
}
