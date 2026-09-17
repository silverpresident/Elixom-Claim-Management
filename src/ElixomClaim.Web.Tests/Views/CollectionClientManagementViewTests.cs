using Xunit;

namespace ElixomClaim.Web.Tests.Views;

public class CollectionClientManagementViewTests
{
    [Fact]
    public void ClientViews_ExposeDiscoverablePurposeAndAmountManagement()
    {
        var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Web", "Views", "CollectionClientsAdmin"));
        var index = File.ReadAllText(Path.Combine(webRoot, "Index.cshtml"));
        var details = File.ReadAllText(Path.Combine(webRoot, "Details.cshtml"));

        Assert.Contains("Manage client", index);
        Assert.Contains("Purpose options", details);
        Assert.Contains("Amount options", details);
        Assert.Contains("UpdatePurpose", details);
        Assert.Contains("SetPurposeActive", details);
        Assert.Contains("UpdateAmount", details);
        Assert.Contains("SetAmountActive", details);
        Assert.Contains("Deactivate an option", details);
    }
}
