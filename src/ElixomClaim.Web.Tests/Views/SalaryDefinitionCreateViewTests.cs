using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class SalaryDefinitionCreateViewTests
{
    [Fact]
    public void CreateView_OrganizesTheSalaryRuleIntoAccessiblePayDateAndScheduleSections()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Payroll", "Create.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("Salary rule details", content);
        Assert.Contains("Payee and pay", content);
        Assert.Contains("Effective dates", content);
        Assert.Contains("Recurrence schedule", content);
        Assert.Contains("asp-for=\"EndDate\"", content);
        Assert.Contains("Create salary definition", content);
    }
}
