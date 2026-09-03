using ElixomClaim.Lib.Services;
namespace ElixomClaim.Web.Mcp.Tools;
public sealed record PayrollPreviewRequest(long SalaryDefinitionId, DateOnly AsOfDate);
public sealed record PayrollRunRequest(long SalaryDefinitionId, DateOnly AsOfDate);
public sealed record PayrollToolResponse(bool Success, string? Error, DateOnly? DueDate, string? Eligibility, decimal? Total, long? PayrollId);
public sealed class PayrollTools
{
    private readonly ISalaryPayrollService _service;
    private readonly IAuditService _audit;
    public PayrollTools(ISalaryPayrollService service, IAuditService audit) { _service = service; _audit = audit; }
    public async Task<PayrollToolResponse> PreviewAsync(PayrollPreviewRequest request, Guid actor, CancellationToken ct) { var result = await _service.PreviewAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_PREVIEW", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.DueDate, result.Value.Eligibility.ToString(), result.Value.ProjectedTotal, null) : new(false, result.Error, null, null, null, null); }
    public async Task<PayrollToolResponse> RunAsync(PayrollRunRequest request, Guid actor, CancellationToken ct) { var result = await _service.GenerateForDefinitionAsync(request.SalaryDefinitionId, actor, request.AsOfDate, ct); await _audit.LogAsync("MCP_PAYROLL_RUN", $"SalaryDefinition:{request.SalaryDefinitionId}", actorUserId: actor.ToString(), isMcpOperation: true, cancellationToken: ct); return result.IsSuccess ? new(true, null, result.Value!.PeriodEndingDate, SalaryGenerationEligibility.Eligible.ToString(), result.Value.PayrollTotal, result.Value.Id) : new(false, result.Error, null, null, null, null); }
}
