using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Data;

public static class DatabaseMigrationExtensions
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);

    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider serviceProvider,
        bool isProduction = false,
        CancellationToken cancellationToken = default)
    {
        await MigrationLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<ApplicationDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            logger?.LogInformation("Checking database state and applying EF Core migrations for schema '{Schema}'...", ApplicationDbContext.DefaultSchema);

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingList = pendingMigrations.ToList();

            if (pendingList.Count > 0)
            {
                logger?.LogInformation("Found {Count} pending migrations: {Migrations}", pendingList.Count, string.Join(", ", pendingList));
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger?.LogInformation("Successfully applied pending database migrations.");
            }
            else
            {
                logger?.LogInformation("Database schema '{Schema}' is up-to-date. No pending migrations.", ApplicationDbContext.DefaultSchema);
            }
        }
        catch (Exception ex)
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<ApplicationDbContext>>();
            logger?.LogError(ex, "An error occurred while applying database migrations.");
            throw;
        }
        finally
        {
            MigrationLock.Release();
        }
    }
}
