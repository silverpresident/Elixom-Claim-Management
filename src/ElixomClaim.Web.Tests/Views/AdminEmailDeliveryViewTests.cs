using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class AdminEmailDeliveryViewTests
{
    [Fact]
    public void DeliveryViews_ShowOnlyVisibleHeaders()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "Admin"));
        var list = File.ReadAllText(Path.Combine(root, "EmailLogs.cshtml"));
        var detail = File.ReadAllText(Path.Combine(root, "EmailLog.cshtml"));

        Assert.Contains("@log.To", list);
        Assert.Contains("@Model.To", detail);
        Assert.Contains("@Model.From", detail);
        Assert.DoesNotContain("Bcc", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bcc", detail, StringComparison.OrdinalIgnoreCase);
    }
}
