using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Web.Configuration;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class AccountControllerTests
{
    private static AccountController CreateController(bool isDevelopment = false, bool developmentTestingEnabled = false)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var environment = new TestHostEnvironment { EnvironmentName = isDevelopment ? "Development" : "Production" };
        return new AccountController(
            db,
            environment,
            Options.Create(new DevelopmentTestingOptions { Enabled = developmentTestingEnabled }),
            NullLogger<AccountController>.Instance);
    }

    [Fact]
    public void Login_ReturnsViewResult_WhenNotAuthenticated()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = controller.Login("/dashboard");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("/dashboard", controller.ViewData["ReturnUrl"]);
    }

    [Fact]
    public void Login_RedirectsToReturnUrl_WhenAlreadyAuthenticated()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test User") }, "Cookie");
        context.User = new ClaimsPrincipal(identity);

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        var result = controller.Login("/dashboard");

        var redirectResult = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirectResult.Url);
    }

    [Fact]
    public void AccessDenied_ReturnsViewResult()
    {
        var controller = CreateController();

        var result = controller.AccessDenied();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task DevelopmentLogin_ReturnsNotFound_OutsideDevelopment()
    {
        var controller = CreateController(isDevelopment: false, developmentTestingEnabled: true);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.DevelopmentLogin(ElixomClaim.Lib.Entities.UserRole.Administrator);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DevelopmentLogin_SignsInTheSelectedActiveRole_InDevelopment()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Users.Add(new ElixomClaim.Lib.Entities.User
        {
            Email = "dev-manager@example.test",
            NormalizedEmail = "DEV-MANAGER@EXAMPLE.TEST",
            FullName = "Development Manager",
            Role = ElixomClaim.Lib.Entities.UserRole.Manager,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        await using var provider = services.BuildServiceProvider();
        var controller = new AccountController(
            db,
            new TestHostEnvironment { EnvironmentName = "Development" },
            Options.Create(new DevelopmentTestingOptions { Enabled = true }),
            NullLogger<AccountController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider }
        };

        var result = await controller.DevelopmentLogin(ElixomClaim.Lib.Entities.UserRole.Manager, "/collections");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/collections", redirect.Url);
        Assert.NotEmpty(controller.Response.Headers.SetCookie.ToString());
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ElixomClaim.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
