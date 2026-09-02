using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface ICollectionService
{
    Task<Result<CollectionTransaction>> RecordAsync(RecordCollectionCommand command, CancellationToken cancellationToken = default);
}

public record RecordCollectionCommand(
    Guid TellerUserId,
    Guid CollectionClientId,
    long PurposeOptionId,
    long AmountOptionId,
    string PayorName,
    string? PayorEmail,
    CollectionMethod Method,
    decimal ProcessingFee,
    DateTime PaymentDateUtc,
    string? ReferenceNumber = null);
