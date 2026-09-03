using ElixomClaim.Lib.Entities;
namespace ElixomClaim.Web.Models;
public sealed class PayrollWorkspaceViewModel
{
    public IReadOnlyList<SalaryDefinition> SalaryDefinitions { get; init; } = [];
    public IReadOnlyList<Payroll> Payrolls { get; init; } = [];
}
