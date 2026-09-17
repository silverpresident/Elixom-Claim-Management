using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class JobPaymentIndexViewTests
{
    [Fact]
    public void IndexView_RendersJobPaymentStatusesWithTheAccessibleColourBadgeComponent()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "JobPayments", "Index.cshtml"));
        var content = File.ReadAllText(path);

        Assert.Contains("Component.InvokeAsync(\"StatusBadge\"", content);
        Assert.Contains("status = job.Status.ToString()", content);
        Assert.DoesNotContain("<td>@job.Status</td>", content);
    }
}
