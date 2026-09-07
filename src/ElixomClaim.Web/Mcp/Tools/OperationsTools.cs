using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ElixomClaim.Web.Mcp.Tools;

public sealed record SalaryGenCommandRequest(Guid SalaryDefinitionId, DateOnly AsOfDate, string IdempotencyKey);
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

[McpServerToolType]
public sealed class OperationsTools
{
    private readonly ISalaryPayrollService _salaryPayrollService;
    private readonly IOutboxService _outboxService;
    private readonly IOperationRecordService _operationRecordService;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;

    public OperationsTools(
        ISalaryPayrollService salaryPayrollService,
        IOutboxService outboxService,
        IOperationRecordService operationRecordService,
        IAuditService audit,
        McpToolActorAccessor actorAccessor)
    {
        _salaryPayrollService = salaryPayrollService;
        _outboxService = outboxService;
        _operationRecordService = operationRecordService;
        _audit = audit;
        _actorAccessor = actorAccessor;
    }

    // Retained for direct domain-adapter unit tests. MCP discovery uses the constructor above.
    public OperationsTools(
        ISalaryPayrollService salaryPayrollService,
        IOutboxService outboxService,
        IOperationRecordService operationRecordService,
        IAuditService audit)
        : this(salaryPayrollService, outboxService, operationRecordService, audit, null!)
    {
    }

    [McpServerTool(Name = "operations_salary_generation"), Description("Request authorized, idempotent salary generation.")]
    public async Task<OperationResponse> RequestSalaryGeneration(SalaryGenCommandRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null) : await RequestSalaryGenerationAsync(actor.Value!.User, request, cancellationToken);
    }

    [McpServerTool(Name = "operations_outbox_wakeup"), Description("Request an authorized, idempotent outbox dispatch wake-up.")]
    public async Task<OperationResponse> RequestOutboxWakeUp(OutboxWakeUpRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null) : await RequestOutboxWakeUpAsync(actor.Value!.User, request, cancellationToken);
    }

    [McpServerTool(Name = "operations_status"), Description("Get the authenticated user's operation status.")]
    public async Task<OperationResponse> GetOperationStatus(OperationStatusRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null) : await GetOperationStatusAsync(actor.Value!.User, request, cancellationToken);
    }

    private static OperationRecordDto MapToDto(OperationRecord record)
    {
        return new OperationRecordDto(
            record.IdempotencyKey,
            record.OperationType,
            record.Status,
            record.Details,
            record.ExecutedAtUtc);
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

        var existing = await _operationRecordService.GetByIdempotencyKeyAsync(key, ct);
        if (existing != null)
        {
            return new OperationResponse(true, "Operation already processed (idempotent).", MapToDto(existing));
        }

        try
        {
            var result = await _salaryPayrollService.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor.Id, request.AsOfDate, ct);
            var status = result.IsSuccess ? "Completed" : "Failed";
            var details = result.IsSuccess ? $"Payroll generated with ID {result.Value?.Id}, total {result.Value?.PayrollTotal} JMD." : result.Error;

            var record = await _operationRecordService.RecordOperationAsync(
                key,
                "SalaryGeneration",
                status,
                details,
                actor.Id.ToString(),
                ct);

            await _audit.LogAsync("MCP_OPERATIONS_SALARY_GEN", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new OperationResponse(result.IsSuccess, result.Error, MapToDto(record));
        }
        catch (Exception ex)
        {
            var record = await _operationRecordService.RecordOperationAsync(
                key,
                "SalaryGeneration",
                "Failed",
                ex.Message,
                actor.Id.ToString(),
                ct);

            return new OperationResponse(false, ex.Message, MapToDto(record));
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

        var existing = await _operationRecordService.GetByIdempotencyKeyAsync(key, ct);
        if (existing != null)
        {
            return new OperationResponse(true, "Operation already processed (idempotent).", MapToDto(existing));
        }

        try
        {
            int batchSize = Math.Clamp(request.BatchSize ?? 25, 1, 100);
            int processed = await _outboxService.DispatchDueAsync(batchSize, ct);

            var record = await _operationRecordService.RecordOperationAsync(
                key,
                "OutboxWakeUp",
                "Completed",
                $"Outbox dispatch executed for batch size {batchSize}; processed {processed} item(s).",
                actor.Id.ToString(),
                ct);

            await _audit.LogAsync("MCP_OPERATIONS_OUTBOX_WAKEUP", $"BatchSize:{batchSize}", actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new OperationResponse(true, null, MapToDto(record));
        }
        catch (Exception ex)
        {
            var record = await _operationRecordService.RecordOperationAsync(
                key,
                "OutboxWakeUp",
                "Failed",
                ex.Message,
                actor.Id.ToString(),
                ct);

            return new OperationResponse(false, ex.Message, MapToDto(record));
        }
    }

    public async Task<OperationResponse> GetOperationStatusAsync(User actor, OperationStatusRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new OperationResponse(false, "IdempotencyKey is required.", null);
        }

        var record = await _operationRecordService.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim(), ct);

        if (record == null)
        {
            return new OperationResponse(false, "Operation record not found.", null);
        }

        return new OperationResponse(true, null, MapToDto(record));
    }
}
