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
}
