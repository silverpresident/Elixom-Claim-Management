using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public interface IApprovedOperationService
{
    Task<Result<OperationRecord>> RequestSalaryGenerationAsync(Guid actorUserId, Guid salaryDefinitionId, DateOnly asOfDate, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Result<OperationRecord>> RequestOutboxWakeUpAsync(Guid actorUserId, int? batchSize, string idempotencyKey, CancellationToken cancellationToken = default);
}
