using System.Collections.Concurrent;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record SalaryGenCommandRequest(long SalaryDefinitionId, DateOnly AsOfDate, string IdempotencyKey);
public sealed record OutboxWakeUpRequest(int? BatchSize, string IdempotencyKey);
public sealed record OperationStatusRequest(string IdempotencyKey);

public sealed record OperationRecordDto(
    string IdempotencyKey,
    string OperationType,
    string Status,
    string? Details,
    DateTime ExecutedAtUtc);

public sealed record OperationResponse(
    bool Success,
    string? Error,
    OperationRecordDto? Record);

public sealed class OperationsTools
{
    private static readonly ConcurrentDictionary<string, OperationRecordDto> _operationStore = new();

    private readonly ISalaryPayrollService _salaryPayrollService;
    private readonly IOutboxService _outboxService;
    private readonly IAuditService _audit;
    private readonly ISystemClock _clock;

    public OperationsTools(
        ISalaryPayrollService salaryPayrollService,
        IOutboxService outboxService,
        IAuditService audit,
        ISystemClock clock)
    {
        _salaryPayrollService = salaryPayrollService;
        _outboxService = outboxService;
        _audit = audit;
        _clock = clock;
    }

    public async Task<OperationResponse> RequestSalaryGenerationAsync(User actor, SalaryGenCommandRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Accountant))
        {
            return new OperationResponse(false, "Access denied. Accountant role required for salary generation operations.", null);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new OperationResponse(false, "IdempotencyKey is required.", null);
        }

        var key = $"salary-gen:{request.SalaryDefinitionId}:{request.IdempotencyKey.Trim()}";

        if (_operationStore.TryGetValue(key, out var existing))
        {
            return new OperationResponse(true, "Operation already processed (idempotent).", existing);
        }

        try
        {
            var result = await _salaryPayrollService.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor.Id, request.AsOfDate, ct);
            var record = new OperationRecordDto(
                key,
                "SalaryGeneration",
                result.IsSuccess ? "Completed" : "Failed",
                result.IsSuccess ? $"Payroll generated with ID {result.Value?.Id}, total {result.Value?.PayrollTotal} JMD." : result.Error,
                _clock.UtcNow);

            _operationStore[key] = record;

            await _audit.LogAsync("MCP_OPERATIONS_SALARY_GEN", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new OperationResponse(result.IsSuccess, result.Error, record);
        }
        catch (Exception ex)
        {
            var failedRecord = new OperationRecordDto(key, "SalaryGeneration", "Failed", ex.Message, _clock.UtcNow);
            _operationStore[key] = failedRecord;
            return new OperationResponse(false, ex.Message, failedRecord);
        }
    }

    public async Task<OperationResponse> RequestOutboxWakeUpAsync(User actor, OutboxWakeUpRequest request, CancellationToken ct)
    {
        if (!actor.Role.HasMinimumRole(UserRole.Administrator))
        {
            return new OperationResponse(false, "Access denied. Administrator role required for outbox dispatch wake-up.", null);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new OperationResponse(false, "IdempotencyKey is required.", null);
        }

        var key = $"outbox-wakeup:{request.IdempotencyKey.Trim()}";

        if (_operationStore.TryGetValue(key, out var existing))
        {
            return new OperationResponse(true, "Operation already processed (idempotent).", existing);
        }

        try
        {
            int batchSize = Math.Clamp(request.BatchSize ?? 25, 1, 100);
            int processed = await _outboxService.DispatchDueAsync(batchSize, ct);

            var record = new OperationRecordDto(
                key,
                "OutboxWakeUp",
                "Completed",
                $"Outbox dispatch executed for batch size {batchSize}; processed {processed} item(s).",
                _clock.UtcNow);

            _operationStore[key] = record;

            await _audit.LogAsync("MCP_OPERATIONS_OUTBOX_WAKEUP", $"BatchSize:{batchSize}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new OperationResponse(true, null, record);
        }
        catch (Exception ex)
        {
            var failedRecord = new OperationRecordDto(key, "OutboxWakeUp", "Failed", ex.Message, _clock.UtcNow);
            _operationStore[key] = failedRecord;
            return new OperationResponse(false, ex.Message, failedRecord);
        }
    }

    public Task<OperationResponse> GetOperationStatusAsync(User actor, OperationStatusRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Task.FromResult(new OperationResponse(false, "IdempotencyKey is required.", null));
        }

        var key = request.IdempotencyKey.Trim();
        var match = _operationStore.Values.FirstOrDefault(o => o.IdempotencyKey.Equals(key, StringComparison.OrdinalIgnoreCase) || o.IdempotencyKey.EndsWith($":{key}", StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            return Task.FromResult(new OperationResponse(false, "Operation record not found.", null));
        }

        return Task.FromResult(new OperationResponse(true, null, match));
    }
}
