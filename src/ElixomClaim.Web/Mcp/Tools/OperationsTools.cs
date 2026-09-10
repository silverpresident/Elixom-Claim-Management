using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly IOperationRecordService _operationRecordService;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;
    private readonly ILogger<OperationsTools> _logger;

    public OperationsTools(
        ISalaryPayrollService salaryPayrollService,
        IOperationRecordService operationRecordService,
        IAuditService audit,
        McpToolActorAccessor actorAccessor,
        ILogger<OperationsTools>? logger = null)
    {
        _salaryPayrollService = salaryPayrollService;
        _operationRecordService = operationRecordService;
        _audit = audit;
        _actorAccessor = actorAccessor;
        _logger = logger ?? NullLogger<OperationsTools>.Instance;
    }

    // Retained for direct domain-adapter unit tests. MCP discovery uses the constructor above.
    public OperationsTools(
        ISalaryPayrollService salaryPayrollService,
        IOperationRecordService operationRecordService,
        IAuditService audit)
        : this(salaryPayrollService, operationRecordService, audit, null!, NullLogger<OperationsTools>.Instance)
    {
    }

    [McpServerTool(Name = "operations_salary_generation"), Description("Request authorized, idempotent salary generation.")]
    public async Task<OperationResponse> RequestSalaryGeneration(SalaryGenCommandRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await RequestSalaryGenerationAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP salary generation operation requested by {ActorId} for definition {SalaryDefinitionId} with success {Success}", actor.Value.User.Id, request.SalaryDefinitionId, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_OPERATIONS_SALARY_GENERATION", new AuditEntity("SalaryDefinition", request.SalaryDefinitionId.ToString()), cancellationToken);
        return response;
    }

    [McpServerTool(Name = "operations_outbox_wakeup"), Description("Request an authorized, idempotent outbox dispatch wake-up.")]
    public async Task<OperationResponse> RequestOutboxWakeUp(OutboxWakeUpRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await RequestOutboxWakeUpAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP outbox wake-up operation requested by {ActorId} with success {Success}", actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_OPERATIONS_OUTBOX_WAKEUP", new AuditEntity("EmailOutbox", "WakeUp"), cancellationToken);
        return response;
    }

    [McpServerTool(Name = "operations_status"), Description("Get the authenticated user's operation status.")]
    public async Task<OperationResponse> GetOperationStatus(OperationStatusRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null);
        var response = await GetOperationStatusAsync(actor.Value!.User, request, cancellationToken);
        _logger.LogInformation("MCP operation status read by {ActorId} with success {Success}", actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_OPERATIONS_STATUS", new AuditEntity("OperationRecord", request.IdempotencyKey), cancellationToken);
        return response;
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

        var key = CreateOperationKey("salary-gen", actor.Id, request.SalaryDefinitionId.ToString("N"), request.IdempotencyKey);
        var reservation = await _operationRecordService.ReserveAsync(key, "SalaryGeneration", actor.Id.ToString(), ct);
        if (!reservation.IsNew)
        {
            return new OperationResponse(true, "Operation already accepted (idempotent).", MapToDto(reservation.Record));
        }

        try
        {
            var result = await _salaryPayrollService.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor.Id, request.AsOfDate, ct);
            var status = result.IsSuccess ? "Completed" : "Failed";
            var details = result.IsSuccess ? $"Payroll generated with ID {result.Value?.Id}, total {result.Value?.PayrollTotal} JMD." : result.Error;

            var record = await _operationRecordService.UpdateStatusAsync(reservation.Record.Id, status, details, ct);

            await _audit.LogAsync("MCP_OPERATIONS_SALARY_GEN", new AuditEntity("OperationRecord", record.Id.ToString()), afterState: new { SalaryDefinitionId = request.SalaryDefinitionId, record.Status }, actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
            return new OperationResponse(result.IsSuccess, result.Error, MapToDto(record));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var record = await _operationRecordService.UpdateStatusAsync(
                reservation.Record.Id, "Failed", "Operation could not be completed.", ct);

            return new OperationResponse(false, "Operation could not be completed.", MapToDto(record));
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

        var key = CreateOperationKey("outbox-wakeup", actor.Id, null, request.IdempotencyKey);
        var reservation = await _operationRecordService.ReserveAsync(key, "OutboxWakeUp", actor.Id.ToString(), ct);
        if (!reservation.IsNew)
        {
            return new OperationResponse(true, "Operation already accepted (idempotent).", MapToDto(reservation.Record));
        }

        var requestedBatchSize = Math.Clamp(request.BatchSize ?? 25, 1, 100);
        var record = await _operationRecordService.UpdateStatusAsync(reservation.Record.Id, "Accepted", $"BatchSize:{requestedBatchSize}", ct);
        // The hosted dispatcher owns provider dispatch. This adapter only persists an
        // auditable wake-up request; the dispatcher will pick up due outbox work on
        // its normal polling cycle rather than MCP invoking worker internals.
        await _audit.LogAsync("MCP_OPERATIONS_OUTBOX_WAKEUP", new AuditEntity("OperationRecord", record.Id.ToString()), afterState: new { BatchSize = requestedBatchSize }, actorUserId: actor.Id.ToString(), isMcpOperation: true, cancellationToken: ct);
        return new OperationResponse(true, "Outbox wake-up request accepted.", MapToDto(record));
    }

    public async Task<OperationResponse> GetOperationStatusAsync(User actor, OperationStatusRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return new OperationResponse(false, "IdempotencyKey is required.", null);
        }

        var record = await _operationRecordService.GetForActorAsync(request.IdempotencyKey.Trim(), actor.Id.ToString(), ct);

        if (record == null)
        {
            return new OperationResponse(false, "Operation record not found.", null);
        }

        return new OperationResponse(true, null, MapToDto(record));
    }

    private static string CreateOperationKey(string operationType, Guid actorUserId, string? target, string idempotencyKey)
    {
        var targetPart = string.IsNullOrEmpty(target) ? string.Empty : $":{target}";
        return $"{operationType}:{actorUserId:N}{targetPart}:{idempotencyKey.Trim()}";
    }
}
