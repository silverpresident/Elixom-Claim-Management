namespace ElixomClaim.Lib.Services;

/// <summary>Consumes durable wake-up requests from the hosted dispatch boundary only.</summary>
public interface IOutboxWakeUpProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
