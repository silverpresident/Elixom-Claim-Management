using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListClaimsRequest(ClaimStatus? StatusFilter = null);
public sealed record GetClaimRequest(Guid ClaimId);
public sealed record SubmitClaimRequest(Guid ClaimId);

public sealed record ClaimDto(
    Guid Id,
    string Title,
    string Description,
    decimal Amount,
    ClaimStatus Status,
    ClaimPaymentStatus PaymentStatus,
    Guid ClaimantUserId,
    DateTime CreatedAtUtc);

public sealed record ClaimListResponse(bool Success, string? Error, List<ClaimDto>? Claims);
public sealed record ClaimDetailResponse(bool Success, string? Error, ClaimDto? Claim);
public sealed record ClaimOperationResponse(bool Success, string? Error);

[McpServerToolType]
public sealed class ClaimTools
{
    private readonly IClaimService _claimService;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;
    private readonly ILogger<ClaimTools> _logger;

    public ClaimTools(IClaimService claimService, IAuditService audit, McpToolActorAccessor actorAccessor, ILogger<ClaimTools>? logger = null)
    {
        _claimService = claimService;
        _audit = audit;
        _actorAccessor = actorAccessor;
        _logger = logger ?? NullLogger<ClaimTools>.Instance;
    }

    // Retained for direct domain-adapter unit tests. MCP discovery uses the constructor above.
    public ClaimTools(IClaimService claimService, IAuditService audit)
        : this(claimService, audit, null!, NullLogger<ClaimTools>.Instance)
    {
    }

    [McpServerTool(Name = "claims_list"), Description("List claims that the authenticated user is permitted to view.")]
    public async Task<ClaimListResponse> ListClaims(ListClaimsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new ClaimListResponse(false, "MCP authorization failed.", null);
        var response = await ListClaimsAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP claim list completed for actor {ActorId} with success {Success}", actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_CLAIMS_LIST", "Claims", cancellationToken);
        return response;
    }

    [McpServerTool(Name = "claims_get"), Description("Get a claim when the authenticated user is permitted to view it.")]
    public async Task<ClaimDetailResponse> GetClaim(GetClaimRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new ClaimDetailResponse(false, "MCP authorization failed.", null);
        var response = await GetClaimAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP claim {ClaimId} read by {ActorId} with success {Success}", request.ClaimId, actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_CLAIMS_GET", $"Claim:{request.ClaimId}", cancellationToken);
        return response;
    }

    [McpServerTool(Name = "claims_submit"), Description("Submit an authenticated user's draft claim.")]
    public async Task<ClaimOperationResponse> SubmitClaim(SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new ClaimOperationResponse(false, "MCP authorization failed.");
        var response = await SubmitClaimAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP claim {ClaimId} submit requested by {ActorId} with success {Success}", request.ClaimId, actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_CLAIMS_SUBMIT", $"Claim:{request.ClaimId}", cancellationToken);
        return response;
    }

    public async Task<ClaimListResponse> ListClaimsAsync(User actor, ListClaimsRequest request, CancellationToken ct)
    {
        try
        {
            List<Claim> claims;
            if (actor.Role.HasMinimumRole(UserRole.Manager))
            {
                claims = await _claimService.GetQueueClaimsAsync(request.StatusFilter, ct);
            }
            else
            {
                claims = await _claimService.GetUserClaimsAsync(actor.Id, ct);
                if (request.StatusFilter.HasValue)
                {
                    claims = claims.Where(c => c.Status == request.StatusFilter.Value).ToList();
                }
            }

            var dtos = claims.Select(c => new ClaimDto(
                c.Id,
                c.Title,
                c.Description,
                c.Amount,
                c.Status,
                c.PaymentStatus,
                c.ClaimantUserId,
                c.CreatedAtUtc
            )).ToList();

            await _audit.LogAsync("MCP_CLAIMS_LIST", $"Actor:{actor.Id}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new ClaimListResponse(true, null, dtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ClaimListResponse(false, "Claims could not be retrieved.", null);
        }
    }

    public async Task<ClaimDetailResponse> GetClaimAsync(User actor, GetClaimRequest request, CancellationToken ct)
    {
        try
        {
            var claim = await _claimService.GetByIdAsync(request.ClaimId, actor, ct);
            if (claim == null)
            {
                return new ClaimDetailResponse(false, "Claim not found or access denied.", null);
            }

            var dto = new ClaimDto(
                claim.Id,
                claim.Title,
                claim.Description,
                claim.Amount,
                claim.Status,
                claim.PaymentStatus,
                claim.ClaimantUserId,
                claim.CreatedAtUtc
            );

            await _audit.LogAsync("MCP_CLAIM_GET", $"Claim:{request.ClaimId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new ClaimDetailResponse(true, null, dto);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ClaimDetailResponse(false, "The claim could not be retrieved.", null);
        }
    }

    public async Task<ClaimOperationResponse> SubmitClaimAsync(User actor, SubmitClaimRequest request, CancellationToken ct)
    {
        try
        {
            var success = await _claimService.SubmitAsync(new SubmitClaimCommand(request.ClaimId, actor.Id), ct);
            if (!success)
            {
                return new ClaimOperationResponse(false, "Failed to submit claim. Only draft claims owned by the user can be submitted.");
            }

            await _audit.LogAsync("MCP_CLAIM_SUBMIT", $"Claim:{request.ClaimId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new ClaimOperationResponse(true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ClaimOperationResponse(false, "The claim could not be submitted.");
        }
    }
}
