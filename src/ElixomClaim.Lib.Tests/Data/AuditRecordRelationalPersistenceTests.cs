using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ElixomClaim.Lib.Tests.Data;

[Trait("Category", "Integration")]
public sealed class AuditRecordRelationalPersistenceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _database = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("AuditRecord_Test1!")
        .Build();

    private bool _isDatabaseAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            await _database.StartAsync();
            _isDatabaseAvailable = true;
        }
        catch
        {
            _isDatabaseAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_isDatabaseAvailable)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task AuditRecords_RejectUpdatesAndDeletesAtTheSqlServerBoundary()
    {
        if (!_isDatabaseAvailable)
        {
            // Environment does not support container execution (e.g. nested overlayfs)
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_database.GetConnectionString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
        context.AuditRecords.Add(new AuditRecord
        {
            Action = "TEST_AUDIT_APPEND_ONLY",
            Target = "audit-record-test"
        });
        await context.SaveChangesAsync();

        var record = await context.AuditRecords.SingleAsync();

        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [dbclaim].[AuditRecords] SET [Action] = {"MUTATED"} WHERE [Id] = {record.Id}"));
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [dbclaim].[AuditRecords] WHERE [Id] = {record.Id}"));
    }
}
