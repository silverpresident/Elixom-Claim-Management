using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;

namespace ElixomClaim.Web.Models;

public sealed class SalaryDefinitionsViewModel
{
    public IReadOnlyList<SalaryDefinition> SalaryDefinitions { get; init; } = [];
    public IReadOnlyDictionary<Guid, SalaryPayrollPreview> Previews { get; init; } = new Dictionary<Guid, SalaryPayrollPreview>();
}
