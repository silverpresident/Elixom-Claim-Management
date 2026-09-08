using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface IJobPaymentService
{
    Task<Result<IReadOnlyList<JobPaymentReadModel>>> ListForActorAsync(Guid actorUserId, JobPaymentStatus? status, int take, CancellationToken cancellationToken = default);
    Task<Result<JobPaymentReadModel>> GetForActorAsync(Guid actorUserId, Guid jobPaymentId, CancellationToken cancellationToken = default);
    Task<Result<int>> QueuePaymentSummaryAsync(Guid jobPaymentId, Guid actorUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Result<JobPayment>> CreateAsync(CreateJobPaymentCommand command, CancellationToken cancellationToken = default);
    Task<Result> UpdateMetadataAsync(UpdateJobPaymentMetadataCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachClaimAsync(AttachJobPaymentClaimCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachClaimsAsync(AttachJobPaymentClaimsCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachCollectionAsync(AttachJobPaymentCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> AttachCollectionsAsync(AttachJobPaymentCollectionsCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveClaimAsync(RemoveJobPaymentClaimCommand command, CancellationToken cancellationToken = default);
    Task<Result> RemoveCollectionAsync(RemoveJobPaymentCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> AddDeductionAsync(AddJobPaymentDeductionCommand command, CancellationToken cancellationToken = default);
    Task<Result> ResendNotificationAsync(Guid jobPaymentId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result> SubmitAsync(Guid jobPaymentId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result> ScheduleAsync(Guid jobPaymentId, Guid actorUserId, DateTime scheduledAtUtc, CancellationToken cancellationToken = default);
    Task<Result> MarkPaidAsync(Guid jobPaymentId, Guid actorUserId, DateTime paymentDateUtc, string transactionNumber, CancellationToken cancellationToken = default);
    Task<Result<JobPayment>> CreateAdjustmentAsync(CreateJobPaymentAdjustmentCommand command, CancellationToken cancellationToken = default);
    Task<Result> ApproveAdjustmentAsync(Guid jobPaymentId, Guid actorUserId, CancellationToken cancellationToken = default);
}

public sealed record JobPaymentReadModel(Guid Id, long SequenceNo, Guid? PayeeUserId, Guid? CollectionClientId, JobPaymentStatus Status, decimal JobTotal, decimal ClientProcessingFee, decimal TotalTxnProcessingFee, decimal TotalDeductions, decimal TotalPaid, string? PublicNote, string? PaymentTransactionNumber, DateTime CreatedAtUtc);

public record CreateJobPaymentCommand(Guid ActorUserId, Guid? PayeeUserId, Guid? CollectionClientId, string? PublicNote, string? InternalNote, string? Title = null);
public record UpdateJobPaymentMetadataCommand(Guid ActorUserId, Guid JobPaymentId, string? Title, string? PublicNote, string? InternalNote);
public record AttachJobPaymentClaimCommand(Guid ActorUserId, Guid JobPaymentId, Guid ClaimId);
public record AttachJobPaymentClaimsCommand(Guid ActorUserId, Guid JobPaymentId, IReadOnlyCollection<Guid> ClaimIds);
public record AttachJobPaymentCollectionCommand(Guid ActorUserId, Guid JobPaymentId, Guid CollectionTransactionId);
public record AttachJobPaymentCollectionsCommand(Guid ActorUserId, Guid JobPaymentId, IReadOnlyCollection<Guid> CollectionTransactionIds);
public record RemoveJobPaymentClaimCommand(Guid ActorUserId, Guid JobPaymentId, Guid ClaimId);
public record RemoveJobPaymentCollectionCommand(Guid ActorUserId, Guid JobPaymentId, Guid CollectionTransactionId);
public record AddJobPaymentDeductionCommand(Guid ActorUserId, Guid JobPaymentId, string Description, decimal Amount);
public record CreateJobPaymentAdjustmentCommand(Guid ActorUserId, Guid OriginalJobPaymentId, decimal Amount, string Reason);
