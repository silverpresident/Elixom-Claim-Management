using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListCollectionsRequest(Guid? CollectionClientId = null);
public sealed record GetCollectionRequest(Guid CollectionId);

public sealed record CollectionDto(
    Guid Id,
    Guid CollectionClientId,
    string PayorName,
    string? PayorEmail,
    CollectionMethod Method,
    CollectionStatus Status,
    decimal Amount,
    decimal ProcessingFee,
    string Currency,
    DateTime PaymentDateUtc,
    DateTime CreatedAtUtc);

public sealed record CollectionListResponse(bool Success, string? Error, List<CollectionDto>? Collections);
public sealed record CollectionDetailResponse(bool Success, string? Error, CollectionDto? Collection);

[McpServerToolType]
public sealed class CollectionTools
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;

    public CollectionTools(ApplicationDbContext dbContext, IAuditService audit, McpToolActorAccessor actorAccessor)
    {
        _dbContext = dbContext;
        _audit = audit;
        _actorAccessor = actorAccessor;
    }

    [McpServerTool(Name = "collections_list"), Description("List collections available to the authenticated teller or manager.")]
    public async Task<CollectionListResponse> ListCollections(ListCollectionsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null) : await ListCollectionsAsync(actor.Value!.User, request, cancellationToken);
    }

    [McpServerTool(Name = "collections_get"), Description("Get a collection available to the authenticated teller or manager.")]
    public async Task<CollectionDetailResponse> GetCollection(GetCollectionRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null) : await GetCollectionAsync(actor.Value!.User, request, cancellationToken);
    }

    public async Task<CollectionListResponse> ListCollectionsAsync(User actor, ListCollectionsRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new CollectionListResponse(false, "Access denied. Teller role or higher is required.", null);
        }

        try
        {
            var query = _dbContext.CollectionTransactions.AsNoTracking();
            if (request.CollectionClientId.HasValue)
            {
                query = query.Where(c => c.CollectionClientId == request.CollectionClientId.Value);
            }

            var collections = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(100)
                .Select(c => new CollectionDto(
                    c.Id,
                    c.CollectionClientId,
                    c.PayorName,
                    c.PayorEmail,
                    c.Method,
                    c.Status,
                    c.Amount,
                    c.ProcessingFee,
                    c.Currency,
                    c.PaymentDateUtc,
                    c.CreatedAtUtc))
                .ToListAsync(ct);

            await _audit.LogAsync("MCP_COLLECTIONS_LIST", $"Actor:{actor.Id}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new CollectionListResponse(true, null, collections);
        }
        catch (Exception ex)
        {
            return new CollectionListResponse(false, ex.Message, null);
        }
    }

    public async Task<CollectionDetailResponse> GetCollectionAsync(User actor, GetCollectionRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Teller))
        {
            return new CollectionDetailResponse(false, "Access denied. Teller role or higher is required.", null);
        }

        try
        {
            var collection = await _dbContext.CollectionTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CollectionId, ct);

            if (collection == null)
            {
                return new CollectionDetailResponse(false, "Collection not found.", null);
            }

            var dto = new CollectionDto(
                collection.Id,
                collection.CollectionClientId,
                collection.PayorName,
                collection.PayorEmail,
                collection.Method,
                collection.Status,
                collection.Amount,
                collection.ProcessingFee,
                collection.Currency,
                collection.PaymentDateUtc,
                collection.CreatedAtUtc);

            await _audit.LogAsync("MCP_COLLECTION_GET", $"Collection:{request.CollectionId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new CollectionDetailResponse(true, null, dto);
        }
        catch (Exception ex)
        {
            return new CollectionDetailResponse(false, ex.Message, null);
        }
    }
}
