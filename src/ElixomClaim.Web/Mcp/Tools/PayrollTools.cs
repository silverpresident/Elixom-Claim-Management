using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
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
    public PayrollTools(ISalaryPayrollService service, IAuditService audit, McpToolActorAccessor actorAccessor) { _service = service; _audit = audit; _actorAccessor = actorAccessor; }

    [McpServerTool(Name = "payroll_preview"), Description("Preview authorized salary payroll generation.")]
    public async Task<PayrollToolResponse> Preview(PayrollPreviewRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null, null, null, null) : await PreviewAsync(request, actor.Value!.User.Id, cancellationToken);
    }

    [McpServerTool(Name = "payroll_run"), Description("Run authorized salary payroll generation.")]
    public async Task<PayrollToolResponse> Run(PayrollRunRequest request, CancellationToken cancellationToken)
    {
        var actor = await _actorAccessor.ResolveAsync(cancellationToken);
        return !actor.IsSuccess ? new(false, "MCP authorization failed.", null, null, null, null) : await RunAsync(request, actor.Value!.User.Id, cancellationToken);
    }
    public async Task<PayrollToolResponse> PreviewAsync(PayrollPreviewRequest request, Guid actor, CancellationToken ct) { var result = await _service.PreviewAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_PREVIEW", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.DueDate, result.Value.Eligibility.ToString(), result.Value.ProjectedTotal, null) : new(false, result.Error, null, null, null, null); }
    public async Task<PayrollToolResponse> RunAsync(PayrollRunRequest request, Guid actor, CancellationToken ct) { var result = await _service.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_RUN", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.PeriodEndingDate, SalaryGenerationEligibility.Eligible.ToString(), result.Value.PayrollTotal, result.Value.Id) : new(false, result.Error, null, null, null, null); }
}
