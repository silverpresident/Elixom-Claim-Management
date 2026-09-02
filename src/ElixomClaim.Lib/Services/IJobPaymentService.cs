using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface IJobPaymentService
{
    Task<Result<JobPayment>> CreateAsync(CreateJobPaymentCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachClaimAsync(AttachJobPaymentClaimCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachCollectionAsync(AttachJobPaymentCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveClaimAsync(RemoveJobPaymentClaimCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveCollectionAsync(RemoveJobPaymentCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> AddDeductionAsync(AddJobPaymentDeductionCommand command, CancellationToken cancellationToken = default);
    Task<Result> ResendNotificationAsync(long jobPaymentId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result> SubmitAsync(long jobPaymentId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result> ScheduleAsync(long jobPaymentId, Guid actorUserId, DateTime scheduledAtUtc, CancellationToken cancellationToken = default);
}

public record CreateJobPaymentCommand(Guid ActorUserId, Guid? PayeeUserId, Guid? CollectionClientId, string? PublicNote, string? InternalNote);
public record AttachJobPaymentClaimCommand(Guid ActorUserId, long JobPaymentId, long ClaimId);
public record AttachJobPaymentCollectionCommand(Guid ActorUserId, long JobPaymentId, long CollectionTransactionId);
public record RemoveJobPaymentClaimCommand(Guid ActorUserId, long JobPaymentId, long ClaimId);
public record RemoveJobPaymentCollectionCommand(Guid ActorUserId, long JobPaymentId, long CollectionTransactionId);
public record AddJobPaymentDeductionCommand(Guid ActorUserId, long JobPaymentId, string Description, decimal Amount);
