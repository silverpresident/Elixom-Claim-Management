using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            // Seed bootstrap administrator if configured
            await SeedBootstrapAdminAsync(scope.ServiceProvider, logger, cancellationToken);
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

    public static async Task SeedBootstrapAdminAsync(
        IServiceProvider serviceProvider,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var authOptions = serviceProvider.GetService<IOptions<AuthenticationOptions>>()?.Value;
        var defaultAdminEmail = authOptions?.DefaultAdminEmail;

        if (string.IsNullOrWhiteSpace(defaultAdminEmail))
        {
            logger?.LogInformation("No DefaultAdminEmail configured. Skipping bootstrap admin seeding.");
            return;
        }

        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var normalizedEmail = defaultAdminEmail.Trim().ToUpperInvariant();

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser == null)
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = defaultAdminEmail.Trim(),
                NormalizedEmail = normalizedEmail,
                FullName = "Bootstrap Administrator",
                Role = UserRole.Administrator,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await dbContext.Users.AddAsync(adminUser, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger?.LogInformation("Seeded bootstrap administrator account for email '{Email}'.", defaultAdminEmail);
        }
        else if (existingUser.Role != UserRole.Administrator || !existingUser.IsActive)
        {
            existingUser.Role = UserRole.Administrator;
            existingUser.IsActive = true;
            existingUser.UpdatedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger?.LogInformation("Promoted existing user '{Email}' to active Administrator.", defaultAdminEmail);
        }
        else
        {
            logger?.LogInformation("Bootstrap administrator '{Email}' already exists and is active.", defaultAdminEmail);
        }
    }
}
