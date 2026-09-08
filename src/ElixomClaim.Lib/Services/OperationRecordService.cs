using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public sealed class OperationRecordService : IOperationRecordService
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemClock _clock;
    private readonly ILogger<OperationRecordService> _logger;

    public OperationRecordService(ApplicationDbContext db, ISystemClock clock, ILogger<OperationRecordService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<OperationRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = idempotencyKey.Trim();
        return await _db.OperationRecords.FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);
    }

    public async Task<OperationRecord?> GetForActorAsync(string idempotencyKey, string actorUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || string.IsNullOrWhiteSpace(actorUserId)) return null;

        return await _db.OperationRecords.FirstOrDefaultAsync(
            o => o.IdempotencyKey == idempotencyKey.Trim() && o.ActorUserId == actorUserId,
            ct);
    }

    public async Task<OperationReservation> ReserveAsync(string idempotencyKey, string operationType, string actorUserId, CancellationToken ct = default)
    {
        var key = idempotencyKey.Trim();
        var existing = await _db.OperationRecords.FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);
        if (existing is not null) return new(existing, false);

        var record = new OperationRecord
        {
            IdempotencyKey = key,
            OperationType = operationType,
            Status = "Accepted",
            ActorUserId = actorUserId,
            ExecutedAtUtc = _clock.UtcNow
        };

        _db.OperationRecords.Add(record);
        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Reserved operation {OperationType} with idempotency key {IdempotencyKey}.", operationType, key);
            return new(record, true);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var raceExisting = await _db.OperationRecords.AsNoTracking().FirstAsync(o => o.IdempotencyKey == key, ct);
            return new(raceExisting, false);
        }
    }

    public async Task<OperationRecord> UpdateStatusAsync(Guid operationId, string status, string? details, CancellationToken ct = default)
    {
        var record = await _db.OperationRecords.SingleAsync(o => o.Id == operationId, ct);
        record.Status = status;
        record.Details = details;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Operation {OperationId} completed with status {Status}.", operationId, status);
        return record;
    }

    public async Task<IReadOnlyList<OperationRecord>> GetPendingOutboxWakeUpsAsync(CancellationToken ct = default)
    {
        var staleBefore = _clock.UtcNow.AddMinutes(-5);
        return await _db.OperationRecords.AsNoTracking()
            .Where(record => record.OperationType == "OutboxWakeUp" &&
                (record.Status == "Accepted" || (record.Status == "Processing" && record.ProcessingStartedAtUtc < staleBefore)))
            .OrderBy(record => record.ExecutedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> TryClaimOutboxWakeUpAsync(Guid operationId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var staleBefore = now.AddMinutes(-5);
        var claimable = _db.OperationRecords.Where(record => record.Id == operationId && record.OperationType == "OutboxWakeUp" &&
            (record.Status == "Accepted" || (record.Status == "Processing" && record.ProcessingStartedAtUtc < staleBefore)));
        if (_db.Database.IsRelational())
        {
            var claimed = await claimable.ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.Status, "Processing")
                .SetProperty(record => record.ProcessingStartedAtUtc, now), ct);
            return claimed == 1;
        }

        // The EF InMemory provider does not support ExecuteUpdate. This retains the
        // lifecycle behavior for non-relational development/test hosts; production
        // SQL uses the conditional set-based claim above.
        var record = await claimable.SingleOrDefaultAsync(ct);
        if (record is null) return false;
        record.Status = "Processing";
        record.ProcessingStartedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<OperationRecord> RecordOperationAsync(
        string idempotencyKey,
        string operationType,
        string status,
        string? details,
        string actorUserId,
        CancellationToken ct = default)
    {
        var key = idempotencyKey.Trim();

        var existing = await _db.OperationRecords
            .FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);

        if (existing != null)
        {
            return existing;
        }

        var record = new OperationRecord
        {
            IdempotencyKey = key,
            OperationType = operationType,
            Status = status,
            Details = details,
            ActorUserId = actorUserId,
            ExecutedAtUtc = _clock.UtcNow
        };

        _db.OperationRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(ct);
            return record;
        }
        catch (DbUpdateException)
        {
            // Concurrent insert race condition fallback: fetch and return the persisted record
            var raceExisting = await _db.OperationRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdempotencyKey == key, ct);

            if (raceExisting != null)
            {
                return raceExisting;
            }

            throw;
        }
    }
}
