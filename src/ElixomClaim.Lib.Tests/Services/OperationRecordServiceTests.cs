using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class OperationRecordServiceTests
{
    private static DbContextOptions<ApplicationDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task OperationRecordService_PersistsAndSurvivesDbContextRestart()
    {
        var options = CreateInMemoryOptions();
        var clock = new SystemClock();

        // Phase 1: Record operation in first DbContext instance
        using (var db1 = new ApplicationDbContext(options))
        {
            var service1 = new OperationRecordService(db1, clock);
            var record1 = await service1.RecordOperationAsync(
                "key-100",
                "OutboxWakeUp",
                "Completed",
                "Processed 5 items",
                "user-123");

            Assert.NotNull(record1);
            Assert.Equal("key-100", record1.IdempotencyKey);
            Assert.Equal("Completed", record1.Status);
        }

        // Phase 2: Query operation status from brand new DbContext instance (simulating restart)
        using (var db2 = new ApplicationDbContext(options))
        {
            var service2 = new OperationRecordService(db2, clock);
            var retrieved = await service2.GetByIdempotencyKeyAsync("key-100");

            Assert.NotNull(retrieved);
            Assert.Equal("key-100", retrieved.IdempotencyKey);
            Assert.Equal("OutboxWakeUp", retrieved.OperationType);
            Assert.Equal("Completed", retrieved.Status);
            Assert.Equal("Processed 5 items", retrieved.Details);
            Assert.Equal("user-123", retrieved.ActorUserId);
        }
    }

    [Fact]
    public async Task OperationRecordService_EnforcesIdempotency()
    {
        var options = CreateInMemoryOptions();
        var clock = new SystemClock();

        using var db = new ApplicationDbContext(options);
        var service = new OperationRecordService(db, clock);

        var first = await service.RecordOperationAsync("key-200", "SalaryGen", "Completed", "Payroll 1", "accountant-1");
        var second = await service.RecordOperationAsync("key-200", "SalaryGen", "Failed", "Different details", "accountant-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Completed", second.Status);
        Assert.Equal("Payroll 1", second.Details);

        var count = await db.OperationRecords.CountAsync(o => o.IdempotencyKey == "key-200");
        Assert.Equal(1, count);
    }
}
