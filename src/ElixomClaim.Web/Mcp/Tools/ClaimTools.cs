using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

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

    public ClaimTools(IClaimService claimService, IAuditService audit, McpToolActorAccessor actorAccessor)
    {
        _claimService = claimService;
        _audit = audit;
        _actorAccessor = actorAccessor;
    }

    // Retained for direct domain-adapter unit tests. MCP discovery uses the constructor above.
    public ClaimTools(IClaimService claimService, IAuditService audit)
        : this(claimService, audit, null!)
    {
    }

    [McpServerTool(Name = "claims_list"), Description("List claims that the authenticated user is permitted to view.")]
    public async Task<ClaimListResponse> ListClaims(ListClaimsRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess
            ? new ClaimListResponse(false, "MCP authorization failed.", null)
            : await ListClaimsAsync(actor.Value!.User, request, cancellationToken);
    }

    [McpServerTool(Name = "claims_get"), Description("Get a claim when the authenticated user is permitted to view it.")]
    public async Task<ClaimDetailResponse> GetClaim(GetClaimRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess
            ? new ClaimDetailResponse(false, "MCP authorization failed.", null)
            : await GetClaimAsync(actor.Value!.User, request, cancellationToken);
    }

    [McpServerTool(Name = "claims_submit"), Description("Submit an authenticated user's draft claim.")]
    public async Task<ClaimOperationResponse> SubmitClaim(SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess
            ? new ClaimOperationResponse(false, "MCP authorization failed.")
            : await SubmitClaimAsync(actor.Value!.User, request, cancellationToken);
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
        catch (Exception ex)
        {
            return new ClaimListResponse(false, ex.Message, null);
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
        catch (Exception ex)
        {
            return new ClaimDetailResponse(false, ex.Message, null);
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
        catch (Exception ex)
        {
            return new ClaimOperationResponse(false, ex.Message);
        }
    }
}
