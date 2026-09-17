using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public sealed class CollectionDetailsViewTests
{
    [Fact]
    public void DetailView_FormatsRecordedAndPaymentDatesInBrowserLocalTime()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Details.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("UtcDateTime", content);
        Assert.DoesNotContain("Intl.DateTimeFormat", content);
        Assert.Contains("Payment Date</dt>", content);
        Assert.DoesNotContain("Payment Date (UTC)", content);
    }

    [Fact]
    public void IndexView_FormatsRecordedDatesInBrowserLocalTime()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Collections", "Index.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("UtcDateTime", content);
        Assert.DoesNotContain("Intl.DateTimeFormat", content);
        Assert.DoesNotContain("CreatedAtUtc.ToString(\"yyyy-MM-dd HH:mm\") UTC", content);
    }

    [Fact]
    public void SharedSiteScript_LocalizesUtcDateElements()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web"));
        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "site.js"));
        var layout = File.ReadAllText(Path.Combine(root, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("[data-utc-date]", script);
        Assert.Contains("Intl.DateTimeFormat", script);
        Assert.Contains("~/js/site.js", layout);

        var template = File.ReadAllText(Path.Combine(root, "Views", "Shared", "DisplayTemplates", "UtcDateTime.cshtml"));
        Assert.Contains("data-utc-date", template);
        Assert.Contains("DateTime.SpecifyKind", template);
    }
}
