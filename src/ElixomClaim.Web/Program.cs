using ElixomClaim.Lib;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Configuration;
using ElixomClaim.Web.Development;
using ElixomClaim.Web.Middleware;
using ElixomClaim.Web.HostedServices;
using ElixomClaim.Web.Mcp.Tools;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var developmentTesting = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>($"{DevelopmentTestingOptions.SectionName}:Enabled");
var developmentDatabaseName = builder.Configuration.GetValue<string>($"{DevelopmentTestingOptions.SectionName}:DatabaseName");

// Register library services, DB context, and options validation
builder.Services.AddClaimLibraryServices(builder.Configuration, developmentTesting, developmentDatabaseName);
builder.Services.AddOptions<DevelopmentTestingOptions>()
    .Bind(builder.Configuration.GetSection(DevelopmentTestingOptions.SectionName));

// Configure Cookie and Google OpenID Connect Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "ElixomClaim.Auth";
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
    options.Events.OnValidatePrincipal = UserValidationEvents.ValidatePrincipalAsync;
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(BearerTokenAuthenticationHandler.SchemeName, _ => { })
.AddGoogle(options =>
{
    var googleOptions = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>();
    options.ClientId = googleOptions?.ClientId ?? "PLACEHOLDER_CLIENT_ID";
    options.ClientSecret = googleOptions?.ClientSecret ?? "PLACEHOLDER_CLIENT_SECRET";
    options.CallbackPath = "/signin-google";
});

// Add MVC controllers with views
builder.Services.AddControllersWithViews();

// MCP clients authenticate only with an OAuth bearer token carrying the MCP scope.
// The tools resolve the concrete active user through IActorResolver on every invocation.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy =>
    {
        policy.AddAuthenticationSchemes(BearerTokenAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains("mcp:access", StringComparer.OrdinalIgnoreCase));
    });
});

// Register shared actor resolver
builder.Services.AddScoped<ElixomClaim.Web.Services.IActorResolver, ElixomClaim.Web.Services.ActorResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<McpToolActorAccessor>();

// Configure Application Rate Limiting / Throttling
builder.Services.AddApplicationRateLimiting();


// Stateless Streamable HTTP binds each MCP invocation to the active HTTP request scope,
// so scoped domain services and the authenticated actor cannot leak across sessions.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<ClaimTools>()
    .WithTools<CollectionTools>()
    .WithTools<JobPaymentTools>()
    .WithTools<PayrollTools>()
    .WithTools<EmailTools>()
    .WithTools<OperationsTools>();

builder.Services.AddHostedService<OutboxDispatchHostedService>();
builder.Services.AddHostedService<SalaryGenerationHostedService>();

var app = builder.Build();

if (developmentTesting)
{
    await DevelopmentDataSeeder.InitializeAsync(app.Services);
}
else
{
    await app.Services.ApplyDatabaseMigrationsAsync(app.Environment.IsProduction());
}

// Correlation ID Middleware first to scope all request logging
app.UseCorrelationId();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// `/mcp` is reserved exclusively for the official Streamable HTTP MCP transport.
app.MapMcp("/mcp")
    .RequireAuthorization("McpAccess")
    .RequireRateLimiting(ElixomClaim.Web.Configuration.RateLimitingConfiguration.McpPolicy);

// Map Health & Readiness endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // Live check passes if application responds
    ResultStatusCodes =
    {
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy] = StatusCodes.Status200OK,
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded] = StatusCodes.Status200OK,
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"), // Readiness check verifies database
    ResultStatusCodes =
    {
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy] = StatusCodes.Status200OK,
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded] = StatusCodes.Status200OK,
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
