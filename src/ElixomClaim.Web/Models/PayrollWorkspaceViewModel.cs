using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
namespace ElixomClaim.Web.Models;
public sealed class PayrollWorkspaceViewModel
{
    public IReadOnlyList<Payroll> Payrolls { get; init; } = [];
    public IReadOnlyList<AuditRecord> AuditRecords { get; init; } = [];
    public bool IsHistory { get; init; }
}
