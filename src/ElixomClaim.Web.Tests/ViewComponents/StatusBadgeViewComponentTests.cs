using ElixomClaim.Web.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Xunit;

namespace ElixomClaim.Web.Tests.ViewComponents;

public class StatusBadgeViewComponentTests
{
    [Theory]
    [InlineData("Draft", "status-badge-draft")]
    [InlineData("Submitted", "status-badge-submitted")]
    [InlineData("Accepted", "status-badge-accepted")]
    [InlineData("Rejected", "status-badge-rejected")]
    [InlineData("Processing", "status-badge-processing")]
    [InlineData("Collected", "status-badge-collected")]
    [InlineData("Scheduled", "status-badge-scheduled")]
    [InlineData("Paid", "status-badge-paid")]
    [InlineData("Honoured", "status-badge-honoured")]
    [InlineData("Transferred", "status-badge-transferred")]
    public void Invoke_ReturnsViewWithCorrectCssClass(string status, string expectedCss)
    {
        var component = new StatusBadgeViewComponent();
        var result = component.Invoke(status);

        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatusBadgeViewModel>(viewResult.ViewData?.Model);

        Assert.Equal(status, model.Status);
        Assert.Equal(expectedCss, model.CssClass);
    }

    [Fact]
    public void Invoke_CustomLabel_UsesCustomLabel()
    {
        var component = new StatusBadgeViewComponent();
        var result = component.Invoke("Paid", "Fully Paid Out");

        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<StatusBadgeViewModel>(viewResult.ViewData?.Model);

        Assert.Equal("Paid", model.Status);
        Assert.Equal("Fully Paid Out", model.Label);
    }
}
