using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Authentication;

public static class UserValidationEvents
{
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<CookieValidatePrincipalContext>>();

        var emailClaim = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrWhiteSpace(emailClaim))
        {
            logger?.LogWarning("Authentication cookie rejected: missing email claim.");
            context.RejectPrincipal();
            return;
        }

        var normalizedEmail = emailClaim.Trim().ToUpperInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (user == null || !user.IsActive || user.Role == UserRole.Blocked)
        {
            logger?.LogWarning("Authentication rejected for email '{Email}'. User active: {IsActive}, Role: {Role}",
                emailClaim, user?.IsActive, user?.Role);

            context.RejectPrincipal();
            return;
        }

        // Refresh role claims and user identity info
        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            var existingRoleClaims = identity.FindAll(ClaimTypes.Role).ToList();
            foreach (var claim in existingRoleClaims)
            {
                identity.RemoveClaim(claim);
            }

            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
            identity.AddClaim(new Claim("UserId", user.Id.ToString()));
        }
    }
}
