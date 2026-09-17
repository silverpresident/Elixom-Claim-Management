using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public sealed class CollectionDetailsViewTests
{
    [Fact]
    public void DetailView_FormatsRecordedAndPaymentDatesInBrowserLocalTime()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Details.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("data-local-date", content);
        Assert.Contains("Intl.DateTimeFormat", content);
        Assert.Contains("Payment Date</dt>", content);
        Assert.DoesNotContain("Payment Date (UTC)", content);
    }

    [Fact]
    public void IndexView_FormatsRecordedDatesInBrowserLocalTime()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Index.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("data-local-date", content);
        Assert.Contains("Intl.DateTimeFormat", content);
        Assert.DoesNotContain("CreatedAtUtc.ToString(\"yyyy-MM-dd HH:mm\") UTC", content);
    }
}
