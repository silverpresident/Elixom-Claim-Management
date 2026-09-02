using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElixomClaim.Lib;

public static class DependencyInjection
{
    public static IServiceCollection AddClaimLibraryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind & Validate Options with DataAnnotations & ValidateOnStart
        services.AddOptions<DatabaseOptions>()
            .Configure(options => configuration.GetSection(DatabaseOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthenticationOptions>()
            .Configure(options => configuration.GetSection(AuthenticationOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GoogleAuthOptions>()
            .Configure(options => configuration.GetSection(GoogleAuthOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<NotificationOptions>()
            .Configure(options => configuration.GetSection(NotificationOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OAuthOptions>()
            .Configure(options => configuration.GetSection(OAuthOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("ClaimDatabase");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.DefaultSchema);
                });
            }
        });

        services.AddSingleton<ISystemClock, SystemClock>();

        // Add EF Core Health Check
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database_health_check",
                tags: new[] { "db", "ready" });

        return services;
    }
}
