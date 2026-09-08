using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface IOperationRecordService
{
    Task<OperationRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<OperationRecord?> GetForActorAsync(string idempotencyKey, string actorUserId, CancellationToken ct = default);
    Task<OperationReservation> ReserveAsync(string idempotencyKey, string operationType, string actorUserId, CancellationToken ct = default);
    Task<OperationRecord> UpdateStatusAsync(Guid operationId, string status, string? details, CancellationToken ct = default);
    Task<OperationRecord> RecordOperationAsync(string idempotencyKey, string operationType, string status, string? details, string actorUserId, CancellationToken ct = default);
}

public sealed record OperationReservation(OperationRecord Record, bool IsNew);
