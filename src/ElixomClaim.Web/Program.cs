using ElixomClaim.Lib;
using ElixomClaim.Lib.Configuration;
using ElixomClaim.Web.Authentication;
using ElixomClaim.Web.Configuration;
using ElixomClaim.Web.Development;
using ElixomClaim.Web.Middleware;
using ElixomClaim.Web.HostedServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

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

// Register domain-scoped MCP tool adapters in DI
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.ClaimTools>();
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.CollectionTools>();
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.JobPaymentTools>();
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.PayrollTools>();
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.EmailTools>();
builder.Services.AddScoped<ElixomClaim.Web.Mcp.Tools.OperationsTools>();

builder.Services.AddHostedService<OutboxDispatchHostedService>();
builder.Services.AddHostedService<SalaryGenerationHostedService>();

var app = builder.Build();

if (developmentTesting)
{
    await DevelopmentDataSeeder.InitializeAsync(app.Services);
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
