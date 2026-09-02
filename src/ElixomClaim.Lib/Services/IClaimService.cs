using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public record CreateClaimCommand(Guid ClaimantUserId, string Title, string Description, decimal Amount);
public record EditClaimCommand(long ClaimId, Guid ActorUserId, string Title, string Description, decimal Amount);
public record SubmitClaimCommand(long ClaimId, Guid ActorUserId);
public record AcceptClaimCommand(long ClaimId, Guid ActorUserId);
public record RejectClaimCommand(long ClaimId, Guid ActorUserId, string RejectionReason);
public record SoftDeleteClaimCommand(long ClaimId, Guid ActorUserId);
public record AddClaimCommentCommand(long ClaimId, Guid AuthorUserId, string Content, bool IsPrivate);

public interface IClaimService
{
    Task<Claim> CreateDraftAsync(CreateClaimCommand command, CancellationToken cancellationToken = default);
    Task<Claim?> EditDraftAsync(EditClaimCommand command, CancellationToken cancellationToken = default);
    Task<bool> SubmitAsync(SubmitClaimCommand command, CancellationToken cancellationToken = default);
    Task<bool> AcceptAsync(AcceptClaimCommand command, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(RejectClaimCommand command, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(SoftDeleteClaimCommand command, CancellationToken cancellationToken = default);
    Task<ClaimComment?> AddCommentAsync(AddClaimCommentCommand command, CancellationToken cancellationToken = default);

    Task<Claim?> GetByIdAsync(long claimId, User actor, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetUserClaimsAsync(Guid claimantUserId, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetQueueClaimsAsync(ClaimStatus? filterStatus = null, CancellationToken cancellationToken = default);
}
