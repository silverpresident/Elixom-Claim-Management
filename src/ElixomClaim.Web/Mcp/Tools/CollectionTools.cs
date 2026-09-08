using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListCollectionsRequest(Guid? CollectionClientId = null);
public sealed record GetCollectionRequest(Guid CollectionId);

public sealed record CollectionDto(
    Guid Id,
    Guid CollectionClientId,
    string PayorName,
    CollectionMethod Method,
    CollectionStatus Status,
    decimal Amount,
    string Currency,
    DateTime PaymentDateUtc,
    DateTime CreatedAtUtc);

public sealed record CollectionListResponse(bool Success, string? Error, List<CollectionDto>? Collections);
public sealed record CollectionDetailResponse(bool Success, string? Error, CollectionDto? Collection);

[McpServerToolType]
public sealed class CollectionTools
{
    private readonly ICollectionService _collections;
    private readonly McpToolActorAccessor _actorAccessor;

    public CollectionTools(ICollectionService collections, McpToolActorAccessor actorAccessor)
    {
        _collections = collections;
        _actorAccessor = actorAccessor;
    }

    [McpServerTool(Name = "collections_list"), Description("List collections available to the authenticated teller or manager.")]
    public async Task<CollectionListResponse> ListCollections(ListCollectionsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await ListCollectionsAsync(actor.Value!.User, request, cancellationToken);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_COLLECTIONS_LIST", "Collections", cancellationToken);
        return response;
    }

    [McpServerTool(Name = "collections_get"), Description("Get a collection available to the authenticated teller or manager.")]
    public async Task<CollectionDetailResponse> GetCollection(GetCollectionRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await GetCollectionAsync(actor.Value!.User, request, cancellationToken);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_COLLECTIONS_GET", $"Collection:{request.CollectionId}", cancellationToken);
        return response;
    }

    public async Task<CollectionListResponse> ListCollectionsAsync(User actor, ListCollectionsRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new CollectionListResponse(false, "Access denied. Teller role or higher is required.", null);
        }

        var result = await _collections.ListForActorAsync(actor.Id, request.CollectionClientId, 100, ct);
        return result.IsSuccess
            ? new CollectionListResponse(true, null, result.Value!.Select(ToDto).ToList())
            : new CollectionListResponse(false, result.Error, null);
    }

    public async Task<CollectionDetailResponse> GetCollectionAsync(User actor, GetCollectionRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new CollectionDetailResponse(false, "Access denied. Teller role or higher is required.", null);
        }

        var result = await _collections.GetForActorAsync(actor.Id, request.CollectionId, ct);
        return result.IsSuccess
            ? new CollectionDetailResponse(true, null, ToDto(result.Value!))
            : new CollectionDetailResponse(false, result.Error, null);
    }

    private static CollectionDto ToDto(CollectionReadModel collection) => new(collection.Id, collection.CollectionClientId, collection.PayorName, collection.Method, collection.Status, collection.Amount, collection.Currency, collection.PaymentDateUtc, collection.CreatedAtUtc);
}
