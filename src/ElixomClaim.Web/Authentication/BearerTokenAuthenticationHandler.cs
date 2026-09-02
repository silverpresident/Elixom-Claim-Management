using System.Security.Claims;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Web.Authentication;

public class BearerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly IOAuthService _oauthService;

    public BearerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOAuthService oauthService)
        : base(options, logger, encoder)
    {
        _oauthService = oauthService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = authHeaderValues.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Empty bearer token");
        }

        var validationResult = await _oauthService.ValidateAccessTokenAsync(token, Context.RequestAborted);
        if (!validationResult.IsValid || validationResult.User == null)
        {
            return AuthenticateResult.Fail(validationResult.Error ?? "Invalid bearer token");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, validationResult.User.Id.ToString()),
            new Claim(ClaimTypes.Email, validationResult.User.Email),
            new Claim(ClaimTypes.Name, validationResult.User.FullName),
            new Claim(ClaimTypes.Role, validationResult.User.Role.ToString()),
            new Claim("client_id", validationResult.ClientId ?? string.Empty)
        };

        if (!string.IsNullOrEmpty(validationResult.Scope))
        {
            claims.Add(new Claim("scope", validationResult.Scope));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
