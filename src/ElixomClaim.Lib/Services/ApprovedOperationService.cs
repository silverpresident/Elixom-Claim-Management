using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public sealed class ApprovedOperationService(ApplicationDbContext db, IOperationRecordService records, ISalaryPayrollService payroll, IAuditService audit, ILogger<ApprovedOperationService> logger) : IApprovedOperationService
{
    public async Task<Result<OperationRecord>> RequestSalaryGenerationAsync(Guid actorUserId, Guid salaryDefinitionId, DateOnly asOfDate, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return Result.Failure<OperationRecord>("An idempotency key is required.");
        var role = await RoleAsync(actorUserId, ct); if (role is not { } value || !value.HasMinimumRole(UserRole.Accountant)) return Result.Failure<OperationRecord>("Accountant access is required.");
        var key = $"salary-gen:{actorUserId:N}:{salaryDefinitionId:N}:{idempotencyKey.Trim()}";
        var reservation = await records.ReserveAsync(key, "SalaryGeneration", actorUserId.ToString(), ct);
        if (!reservation.IsNew) return Result.Success(reservation.Record);
        try
        {
            var generated = await payroll.GenerateForDefinitionAsync(salaryDefinitionId, actorUserId, asOfDate, ct);
            var record = await records.UpdateStatusAsync(reservation.Record.Id, generated.IsSuccess ? "Completed" : "Failed", generated.IsSuccess ? "Salary generation completed." : generated.Error, ct);
            await audit.LogAsync("OPERATION_SALARY_GENERATION_REQUESTED", $"SalaryDefinition:{salaryDefinitionId}", actorUserId: actorUserId.ToString(), cancellationToken: ct);
            logger.LogInformation("Approved salary generation operation {OperationId} completed with status {Status}", record.Id, record.Status);
            return generated.IsSuccess ? Result.Success(record) : Result.Failure<OperationRecord>(generated.Error);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            var record = await records.UpdateStatusAsync(reservation.Record.Id, "Failed", "Operation could not be completed.", ct);
            logger.LogWarning("Approved salary generation operation {OperationId} failed", record.Id);
            return Result.Failure<OperationRecord>("Operation could not be completed.");
        }
    }

    public async Task<Result<OperationRecord>> RequestOutboxWakeUpAsync(Guid actorUserId, int? batchSize, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return Result.Failure<OperationRecord>("An idempotency key is required.");
        if (await RoleAsync(actorUserId, ct) is not UserRole.Administrator) return Result.Failure<OperationRecord>("Administrator access is required.");
        var key = $"outbox-wakeup:{actorUserId:N}:{idempotencyKey.Trim()}";
        var reservation = await records.ReserveAsync(key, "OutboxWakeUp", actorUserId.ToString(), ct);
        if (reservation.IsNew) await audit.LogAsync("OPERATION_OUTBOX_WAKEUP_REQUESTED", $"BatchSize:{Math.Clamp(batchSize ?? 25, 1, 100)}", actorUserId: actorUserId.ToString(), cancellationToken: ct);
        logger.LogInformation("Approved outbox wake-up operation {OperationId} accepted for actor {ActorId}", reservation.Record.Id, actorUserId);
        return Result.Success(reservation.Record);
    }

    private async Task<UserRole?> RoleAsync(Guid actorUserId, CancellationToken ct) => await db.Users.Where(user => user.Id == actorUserId && user.IsActive).Select(user => (UserRole?)user.Role).SingleOrDefaultAsync(ct);
}
