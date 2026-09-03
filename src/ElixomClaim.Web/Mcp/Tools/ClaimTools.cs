using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record ListClaimsRequest(ClaimStatus? StatusFilter = null);
public sealed record GetClaimRequest(long ClaimId);
public sealed record SubmitClaimRequest(long ClaimId);

public sealed record ClaimDto(
    long Id,
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

public sealed class ClaimTools
{
    private readonly IClaimService _claimService;
    private readonly IAuditService _audit;

    public ClaimTools(IClaimService claimService, IAuditService audit)
    {
        _claimService = claimService;
        _audit = audit;
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
