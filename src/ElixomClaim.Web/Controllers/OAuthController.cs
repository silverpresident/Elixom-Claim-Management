using System.Security.Claims;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElixomClaim.Web.Controllers;

[Route("oauth")]
public class OAuthController : Controller
{
    private readonly IOAuthService _oauthService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(IOAuthService oauthService, ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _logger = logger;
    }

    public record ClientRegistrationRequest(string client_name, List<string> redirect_uris);

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] ClientRegistrationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.client_name) || request.redirect_uris == null || !request.redirect_uris.Any())
        {
            return BadRequest(new { error = "invalid_client_metadata", error_description = "client_name and redirect_uris are required" });
        }

        var result = await _oauthService.RegisterClientAsync(request.client_name, request.redirect_uris);

        return Created("", new
        {
            client_id = result.ClientId,
            client_secret = result.ClientSecret,
            client_name = result.ClientName,
            redirect_uris = result.RedirectUris,
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "client_secret_post"
        });
    }

    [HttpGet("authorize")]
    [Authorize]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery(Name = "scope")] string scope,
        [FromQuery(Name = "state")] string state,
        [FromQuery(Name = "code_challenge")] string codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string codeChallengeMethod = "S256")
    {
        if (responseType != "code")
        {
            return BadRequest(new { error = "unsupported_response_type" });
        }

        var client = await _oauthService.GetClientAsync(clientId);
        if (client == null)
        {
            return BadRequest(new { error = "invalid_client" });
        }

        var isValidRedirect = await _oauthService.ValidateRedirectUriAsync(clientId, redirectUri);
        if (!isValidRedirect)
        {
            return BadRequest(new { error = "invalid_redirect_uri" });
        }

        if (string.IsNullOrWhiteSpace(codeChallenge) || !string.Equals(codeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "invalid_request", error_description = "PKCE S256 code_challenge is required" });
        }

        ViewBag.ClientName = client.ClientName;
        ViewBag.ClientId = clientId;
        ViewBag.RedirectUri = redirectUri;
        ViewBag.Scope = scope ?? "openid profile email mcp:access";
        ViewBag.State = state;
        ViewBag.CodeChallenge = codeChallenge;
        ViewBag.CodeChallengeMethod = codeChallengeMethod;

        return View();
    }

    [HttpPost("authorize")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuthorizeConsent(
        [FromForm] string clientId,
        [FromForm] string redirectUri,
        [FromForm] string scope,
        [FromForm] string state,
        [FromForm] string codeChallenge,
        [FromForm] string codeChallengeMethod,
        [FromForm] string submitAction)
    {
        if (submitAction != "Approve")
        {
            return Redirect($"{redirectUri}?error=access_denied&state={Uri.EscapeDataString(state ?? "")}");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var code = await _oauthService.CreateAuthorizationCodeAsync(
            clientId, userId, redirectUri, scope ?? "openid profile email mcp:access", codeChallenge, codeChallengeMethod);

        var redirectUrl = $"{redirectUri}?code={code}";
        if (!string.IsNullOrEmpty(state))
        {
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";
        }

        return Redirect(redirectUrl);
    }

    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token(
        [FromForm(Name = "grant_type")] string grantType,
        [FromForm(Name = "code")] string? code,
        [FromForm(Name = "client_id")] string clientId,
        [FromForm(Name = "client_secret")] string? clientSecret,
        [FromForm(Name = "redirect_uri")] string? redirectUri,
        [FromForm(Name = "code_verifier")] string? codeVerifier,
        [FromForm(Name = "refresh_token")] string? refreshToken)
    {
        if (grantType == "authorization_code")
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeVerifier))
            {
                return BadRequest(new { error = "invalid_request", error_description = "code, redirect_uri, and code_verifier are required" });
            }

            var tokenResult = await _oauthService.ExchangeCodeForTokensAsync(code, clientId, clientSecret, redirectUri, codeVerifier);
            if (tokenResult == null)
            {
                return BadRequest(new { error = "invalid_grant" });
            }

            return Ok(new
            {
                access_token = tokenResult.AccessToken,
                token_type = tokenResult.TokenType,
                expires_in = tokenResult.ExpiresIn,
                refresh_token = tokenResult.RefreshToken,
                scope = tokenResult.Scope
            });
        }
        else if (grantType == "refresh_token")
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });
            }

            var tokenResult = await _oauthService.RefreshTokenAsync(refreshToken, clientId, clientSecret);
            if (tokenResult == null)
            {
                return BadRequest(new { error = "invalid_grant" });
            }

            return Ok(new
            {
                access_token = tokenResult.AccessToken,
                token_type = tokenResult.TokenType,
                expires_in = tokenResult.ExpiresIn,
                refresh_token = tokenResult.RefreshToken,
                scope = tokenResult.Scope
            });
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }

    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromForm] string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { error = "invalid_request", error_description = "token is required" });
        }

        await _oauthService.RevokeTokenAsync(token);
        return Ok();
    }
}
