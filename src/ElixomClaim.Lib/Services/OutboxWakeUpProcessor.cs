using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public sealed class OutboxWakeUpProcessor(
    IOperationRecordService records,
    IOutboxService outbox,
    ILogger<OutboxWakeUpProcessor> logger) : IOutboxWakeUpProcessor
{
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await records.GetPendingOutboxWakeUpsAsync(cancellationToken);
        var completed = 0;
        foreach (var request in pending)
        {
            if (!await records.TryClaimOutboxWakeUpAsync(request.Id, cancellationToken)) continue;
            try
            {
                var dispatched = await outbox.DispatchDueAsync(ParseBatchSize(request.Details), cancellationToken);
                await records.UpdateStatusAsync(request.Id, "Completed", $"Hosted dispatcher processed {dispatched} due outbox item(s).", cancellationToken);
                logger.LogInformation("Completed durable outbox wake-up operation {OperationId} with {Count} processed items", request.Id, dispatched);
                completed++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                await records.UpdateStatusAsync(request.Id, "Failed", "Hosted dispatch could not complete the request.", cancellationToken);
                logger.LogWarning("Durable outbox wake-up operation {OperationId} failed", request.Id);
            }
        }
        return completed;
    }

    private static int ParseBatchSize(string? details)
    {
        const string prefix = "BatchSize:";
        if (details?.StartsWith(prefix, StringComparison.Ordinal) == true && int.TryParse(details[prefix.Length..], out var batchSize))
            return Math.Clamp(batchSize, 1, 100);
        return 25;
    }
}
