using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
            var service1 = new OperationRecordService(db1, clock, NullLogger<OperationRecordService>.Instance);
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
            var service2 = new OperationRecordService(db2, clock, NullLogger<OperationRecordService>.Instance);
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
        var service = new OperationRecordService(db, clock, NullLogger<OperationRecordService>.Instance);

        var first = await service.RecordOperationAsync("key-200", "SalaryGen", "Completed", "Payroll 1", "accountant-1");
        var second = await service.RecordOperationAsync("key-200", "SalaryGen", "Failed", "Different details", "accountant-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Completed", second.Status);
        Assert.Equal("Payroll 1", second.Details);

        var count = await db.OperationRecords.CountAsync(o => o.IdempotencyKey == "key-200");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task OutboxWakeUpProcessor_RecoversStalePersistedRequestAfterRestart()
    {
        var options = CreateInMemoryOptions();
        var clock = new SystemClock();
        Guid operationId;
        using (var db1 = new ApplicationDbContext(options))
        {
            var records = new OperationRecordService(db1, clock, NullLogger<OperationRecordService>.Instance);
            var reservation = await records.ReserveAsync("wake-up-100", "OutboxWakeUp", "admin-1");
            operationId = reservation.Record.Id;
            await records.UpdateStatusAsync(operationId, "Processing", "BatchSize:7");
            var interrupted = await db1.OperationRecords.SingleAsync(record => record.Id == operationId);
            interrupted.ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-6);
            await db1.SaveChangesAsync();
        }

        using (var db2 = new ApplicationDbContext(options))
        {
            var records = new OperationRecordService(db2, clock, NullLogger<OperationRecordService>.Instance);
            var outbox = new StubOutboxService();
            var processor = new OutboxWakeUpProcessor(records, outbox, NullLogger<OutboxWakeUpProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessPendingAsync());
            var record = await records.GetByIdempotencyKeyAsync("wake-up-100");
            Assert.Equal("Completed", record!.Status);
            Assert.Contains("3 due outbox", record.Details);
            Assert.Equal(7, outbox.BatchSize);
        }
    }

    private sealed class StubOutboxService : IOutboxService
    {
        public int BatchSize { get; private set; }
        public Task<int> DispatchDueAsync(int batchSize = 25, CancellationToken cancellationToken = default)
        {
            BatchSize = batchSize;
            return Task.FromResult(3);
        }
    }
}
