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
