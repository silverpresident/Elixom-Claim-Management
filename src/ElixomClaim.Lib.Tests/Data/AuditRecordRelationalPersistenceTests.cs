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
        .WithPassword("AuditRecord_Test1!")
        .Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task AuditRecords_RejectUpdatesAndDeletesAtTheSqlServerBoundary()
    {
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
