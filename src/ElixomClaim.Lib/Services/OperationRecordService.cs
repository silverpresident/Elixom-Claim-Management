using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Lib.Services;

public sealed class OperationRecordService : IOperationRecordService
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemClock _clock;

    public OperationRecordService(ApplicationDbContext db, ISystemClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OperationRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = idempotencyKey.Trim();
        return await _db.OperationRecords
            .FirstOrDefaultAsync(o => o.IdempotencyKey == key || o.IdempotencyKey.EndsWith($":{key}"), ct);
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
