using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
namespace ElixomClaim.Web.Models;
public sealed class PayrollWorkspaceViewModel
{
    public IReadOnlyList<SalaryDefinition> SalaryDefinitions { get; init; } = [];
    public IReadOnlyList<Payroll> Payrolls { get; init; } = [];
    public IReadOnlyList<AuditRecord> AuditRecords { get; init; } = [];
    public IReadOnlyDictionary<long, SalaryPayrollPreview> Previews { get; init; } = new Dictionary<long, SalaryPayrollPreview>();
}
