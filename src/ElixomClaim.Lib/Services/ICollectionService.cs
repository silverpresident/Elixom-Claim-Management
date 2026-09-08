using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface ICollectionService
{
    Task<Result<CollectionTransaction>> RecordAsync(RecordCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> ReissueReceiptAsync(Guid collectionId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CollectionReadModel>>> ListForActorAsync(Guid actorUserId, Guid? collectionClientId, int take, CancellationToken cancellationToken = default);
    Task<Result<CollectionReadModel>> GetForActorAsync(Guid actorUserId, Guid collectionId, CancellationToken cancellationToken = default);
    Task<Result<int>> QueueReceiptAsync(Guid collectionId, Guid actorUserId, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed record CollectionReadModel(Guid Id, long SequenceNo, Guid CollectionClientId, string PayorName, CollectionMethod Method, CollectionStatus Status, decimal Amount, string Currency, DateTime PaymentDateUtc, DateTime CreatedAtUtc);

public record RecordCollectionCommand(
    Guid TellerUserId,
    Guid CollectionClientId,
    Guid? PurposeOptionId,
    Guid? AmountOptionId,
    string PayorName,
    string? PayorEmail,
    CollectionMethod Method,
    decimal ProcessingFee,
    DateTime PaymentDateUtc,
    string? ReferenceNumber = null,
    string? PayorTelephone = null,
    string? Purpose = null,
    decimal? Amount = null);
