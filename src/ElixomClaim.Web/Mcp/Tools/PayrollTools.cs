using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace ElixomClaim.Web.Mcp.Tools;

public sealed record PayrollPreviewRequest(Guid SalaryDefinitionId, DateOnly AsOfDate);
public sealed record PayrollRunRequest(Guid SalaryDefinitionId, DateOnly AsOfDate);
public sealed record PayrollToolResponse(bool Success, string? Error, DateOnly? DueDate, string? Eligibility, decimal? Total, Guid? PayrollId);
[McpServerToolType]
public sealed class PayrollTools
{
    private readonly ISalaryPayrollService _service;
    private readonly IAuditService _audit;
    private readonly McpToolActorAccessor _actorAccessor;
    private readonly ILogger<PayrollTools> _logger;
    public PayrollTools(ISalaryPayrollService service, IAuditService audit, McpToolActorAccessor actorAccessor, ILogger<PayrollTools>? logger = null) { _service = service; _audit = audit; _actorAccessor = actorAccessor; _logger = logger ?? NullLogger<PayrollTools>.Instance; }

    [McpServerTool(Name = "payroll_preview"), Description("Preview authorized salary payroll generation.")]
    public async Task<PayrollToolResponse> Preview(PayrollPreviewRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null, null, null, null);
        var response = await PreviewAsync(request, actor.Value!.User.Id, cancellationToken);
        _logger.LogInformation("MCP payroll preview completed for salary definition {SalaryDefinitionId} and actor {ActorId} with success {Success}", request.SalaryDefinitionId, actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_PAYROLL_PREVIEW", new AuditEntity("SalaryDefinition", request.SalaryDefinitionId.ToString()), cancellationToken);
        return response;
    }

    [McpServerTool(Name = "payroll_run"), Description("Run authorized salary payroll generation.")]
    public async Task<PayrollToolResponse> Run(PayrollRunRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        if (!actor.IsSuccess) return new(false, "MCP authorization failed.", null, null, null, null);
        var response = await RunAsync(request, actor.Value!.User.Id, cancellationToken);
        _logger.LogInformation("MCP payroll run completed for salary definition {SalaryDefinitionId} and actor {ActorId} with success {Success}", request.SalaryDefinitionId, actor.Value.User.Id, response.Success);
        await _actorAccessor.AuditAsync(actor.Value, "MCP_TOOL_PAYROLL_RUN", new AuditEntity("SalaryDefinition", request.SalaryDefinitionId.ToString()), cancellationToken);
        return response;
    }
    public async Task<PayrollToolResponse> PreviewAsync(PayrollPreviewRequest request, Guid actor, CancellationToken ct) { var result = await _service.PreviewAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_PREVIEW", new AuditEntity("SalaryDefinition", request.SalaryDefinitionId.ToString()), actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.DueDate, result.Value.Eligibility.ToString(), result.Value.ProjectedTotal, null) : new(false, result.Error, null, null, null, null); }
    public async Task<PayrollToolResponse> RunAsync(PayrollRunRequest request, Guid actor, CancellationToken ct) { var result = await _service.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_RUN", new AuditEntity("SalaryDefinition", request.SalaryDefinitionId.ToString()), actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.PeriodEndingDate, SalaryGenerationEligibility.Eligible.ToString(), result.Value.PayrollTotal, result.Value.Id) : new(false, result.Error, null, null, null, null); }
}
