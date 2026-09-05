using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ElixomClaim.Web.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ElixomClaim.Web.Tests.Middleware;

public class RateLimitingTests
{
    [Fact]
    public void GetClientIdentityKey_PrefersUserId_WhenUserIsAuthenticated()
    {
        var context = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        var key = RateLimitingConfiguration.GetClientIdentityKey(context, "test");

        Assert.Equal("test:user:user-123", key);
    }

    [Fact]
    public void GetClientIdentityKey_UsesRemoteIp_WhenUnauthenticated()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var key = RateLimitingConfiguration.GetClientIdentityKey(context, "test");

        Assert.Equal("test:ip:10.0.0.1", key);
    }

    [Fact]
    public void GetClientIdentityKey_FallsBackToAnonymous_WhenNoUserOrIp()
    {
        var context = new DefaultHttpContext();

        var key = RateLimitingConfiguration.GetClientIdentityKey(context, "test");

        Assert.Equal("test:anonymous", key);
    }

    [Fact]
    public async Task RateLimiter_EnforcesLimitAndReturnsSafe429Response()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddApplicationRateLimiting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseRateLimiter();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/limited", () => "OK")
                                     .RequireRateLimiting(RateLimitingConfiguration.OAuthPolicy);
                        });
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // OAuth policy permit limit is 20 requests per minute
        for (int i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/limited");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // The 21st request should be rate limited with HTTP 429
        var rejectedResponse = await client.GetAsync("/limited");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);

        var body = await rejectedResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Equal("too_many_requests", body["error"]);
        Assert.Equal("Rate limit exceeded. Please try again later.", body["error_description"]);
    }
}
