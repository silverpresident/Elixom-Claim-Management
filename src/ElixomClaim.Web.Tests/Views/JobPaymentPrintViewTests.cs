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
}
