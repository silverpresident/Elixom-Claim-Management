using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface ICollectionService
{
    Task<Result<CollectionTransaction>> RecordAsync(RecordCollectionCommand command, CancellationToken cancellationToken = default);
    Task<Result> ReissueReceiptAsync(Guid collectionId, Guid actorUserId, CancellationToken cancellationToken = default);
}

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
