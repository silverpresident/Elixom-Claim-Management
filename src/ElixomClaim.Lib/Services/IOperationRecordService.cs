using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface IOperationRecordService
{
    Task<OperationRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<OperationRecord> RecordOperationAsync(string idempotencyKey, string operationType, string status, string? details, string actorUserId, CancellationToken ct = default);
}
