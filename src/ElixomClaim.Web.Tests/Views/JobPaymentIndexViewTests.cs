using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class JobPaymentIndexViewTests
{
    [Fact]
    public void IndexView_UsesTheBootstrapStatusBadgeSchemeUsedByCollectionAndPaymentLists()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "JobPayments", "Index.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("text-bg-secondary", content);
        Assert.Contains("text-bg-info", content);
        Assert.Contains("text-bg-warning", content);
        Assert.Contains("text-bg-success", content);
        Assert.Contains("<span class=\"badge @statusCss\">@job.Status</span>", content);
        Assert.DoesNotContain("<td>@job.Status</td>", content);
    }
}
