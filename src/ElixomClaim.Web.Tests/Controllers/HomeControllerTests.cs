using Microsoft.AspNetCore.Mvc;
using ElixomClaim.Lib.Data;
using ElixomClaim.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class HomeControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Privacy_ReturnsViewResult()
    {
        var db = CreateInMemoryDbContext();
        var controller = new HomeController(db, NullLogger<HomeController>.Instance);

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void PrivacyViewContent_ContainsRequiredPrivacyPolicyDetails()
    {
        var privacyFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "ElixomClaim.Web",
            "Views", "Home", "Privacy.cshtml");

        Assert.True(File.Exists(privacyFilePath), $"Privacy view not found at path: {privacyFilePath}");

        var content = File.ReadAllText(privacyFilePath);

        Assert.Contains("Jamaica", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Data Protection Act", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("privacy@elixom.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nine (9) years", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four (4) years", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legal review", content, StringComparison.OrdinalIgnoreCase);
    }
}
