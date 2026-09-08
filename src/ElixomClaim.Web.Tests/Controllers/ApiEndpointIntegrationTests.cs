using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers.Api;
using ElixomClaim.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Web.Tests.Controllers;

public class ApiEndpointIntegrationTests
{
    [Fact]
    public async Task ClaimsApi_RequiresApiScope_AndReturnsOwnedClaims()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        using var host = await CreateHostAsync(databaseName);
        var userId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = userId, Email = "api-user@example.test", NormalizedEmail = "API-USER@EXAMPLE.TEST", FullName = "API User", Role = UserRole.User, IsActive = true });
            db.Claims.Add(new ElixomClaim.Lib.Entities.Claim { ClaimantUserId = userId, Title = "Owned", Description = "Owned claim", Amount = 10m, Status = ClaimStatus.Draft, PaymentStatus = ClaimPaymentStatus.Unpaid, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/claims")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Scope", "mcp:access");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/claims")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Scope");
        client.DefaultRequestHeaders.Add("X-Test-Scope", "api:access");
        var response = await client.GetAsync("/api/v1/claims?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Owned", await response.Content.ReadAsStringAsync());
    }

    private static async Task<IHost> CreateHostAsync(string databaseName) => await new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer().ConfigureServices(services =>
    {
        services.AddLogging(); services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<ElixomClaim.Lib.Services.ISystemClock, ElixomClaim.Lib.Services.SystemClock>(); services.AddScoped<IAuditService, AuditService>(); services.AddScoped<IClaimService, ClaimService>(); services.AddScoped<IOperationRecordService, OperationRecordService>(); services.AddScoped<IActorResolver, ActorResolver>(); services.AddHttpContextAccessor();
        services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        services.AddAuthorization(options => options.AddPolicy("ApiAccess", policy => { policy.AddAuthenticationSchemes("Test"); policy.RequireAuthenticatedUser(); policy.RequireAssertion(context => context.User.HasClaim("scope", "api:access")); }));
        services.AddControllers().AddApplicationPart(typeof(ClaimsApiController).Assembly);
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).StartAsync();

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var id = Request.Headers["X-Test-User"].FirstOrDefault(); if (!Guid.TryParse(id, out _)) return Task.FromResult(AuthenticateResult.NoResult());
            var scope = Request.Headers["X-Test-Scope"].FirstOrDefault() ?? string.Empty;
            var identity = new ClaimsIdentity([new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, id!), new System.Security.Claims.Claim("scope", scope)], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
