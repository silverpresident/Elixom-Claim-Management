using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Microsoft.AspNetCore.RateLimiting;

namespace ElixomClaim.Web.Controllers;

[EnableRateLimiting(RateLimitingConfiguration.MvcPolicy)]
public class AccountController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly DevelopmentTestingOptions _developmentTesting;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ApplicationDbContext dbContext,
        IHostEnvironment environment,
        IOptions<DevelopmentTestingOptions> developmentTesting,
        ILogger<AccountController> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _developmentTesting = developmentTesting.Value;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevelopmentLogin(UserRole role, string? returnUrl = null)
    {
        if (!_environment.IsDevelopment() || !_developmentTesting.Enabled || role == UserRole.Blocked)
        {
            return NotFound();
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(user => user.Role == role && user.IsActive);
        if (user is null)
        {
            return NotFound();
        }

        var claims = new[]
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Name, user.FullName),
            new System.Security.Claims.Claim(ClaimTypes.Email, user.Email),
            new System.Security.Claims.Claim(ClaimTypes.Role, user.Role.ToString()),
            new System.Security.Claims.Claim("UserId", user.Id.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        _logger.LogInformation("Development testing login issued for role {Role} and user {UserId}.", user.Role, user.Id);

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return RedirectToAction(nameof(AccessDenied));
        }

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
