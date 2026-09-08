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
