using System.Security.Claims;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class AccountControllerTests
{
    [Fact]
    public void Login_ReturnsViewResult_WhenNotAuthenticated()
    {
        var controller = new AccountController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
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

        var controller = new AccountController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };

        var result = controller.Login("/dashboard");

        var redirectResult = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/dashboard", redirectResult.Url);
    }

    [Fact]
    public void AccessDenied_ReturnsViewResult()
    {
        var controller = new AccountController();

        var result = controller.AccessDenied();

        Assert.IsType<ViewResult>(result);
    }
}
