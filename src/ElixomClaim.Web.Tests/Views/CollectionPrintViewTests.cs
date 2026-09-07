using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class CollectionPrintViewTests
{
    [Fact]
    public void PrintReceipt_IsHtmlAndOmitsInternalFee()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Print.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("<article", content);
        Assert.Contains("window.print()", content);
        Assert.DoesNotContain("ProcessingFee", content);
        Assert.DoesNotContain("pdf", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrintReceipt_IncludesPermittedReceiptDetailsAndLocalTimeFormatting()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Print.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("Model.TellerUser.DisplayName", content);
        Assert.Contains("Model.PayorName", content);
        Assert.Contains("Model.PayorEmail", content);
        Assert.Contains("Intl.DateTimeFormat", content);
        Assert.Contains("local-payment-date", content);
        Assert.DoesNotContain("PayorTelephone", content);
    }
}
