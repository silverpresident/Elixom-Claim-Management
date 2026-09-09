using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class JobPaymentPrintViewTests
{
    [Fact]
    public void PrintView_OmitsInternalNotesAndUsesHtmlPrintSemantics()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "JobPayments", "Print.cshtml"));
        var content = File.ReadAllText(path);
        Assert.Contains("<article", content);
        Assert.Contains("window.print()", content);
        Assert.DoesNotContain("InternalNote", content);
    }

    [Fact]
    public void PrintView_IncludesEveryPermittedJobPaymentItemType()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "JobPayments", "Print.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("Collection transactions", content);
        Assert.Contains("Payor</th>", content);
        Assert.Contains("PayorEmail", content);
        Assert.DoesNotContain("PayorTelephone", content);
        Assert.Contains("Linked payrolls", content);
        Assert.Contains("Deductions", content);
        Assert.Contains("Adjustment", content);
        Assert.Contains("Total calculation", content);
    }

    [Fact]
    public void PrintView_UsesAccessibleResponsiveTablesAndCategorySubtotals()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "JobPayments", "Print.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("table-responsive", content);
        Assert.Contains("<caption>", content);
        Assert.Contains("scope=\"col\"", content);
        Assert.Contains("Claims subtotal", content);
        Assert.Contains("Collections subtotal", content);
        Assert.Contains("Payrolls subtotal", content);
        Assert.Contains("Deductions subtotal", content);
    }
}
