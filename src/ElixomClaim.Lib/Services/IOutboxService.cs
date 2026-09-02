namespace ElixomClaim.Lib.Services;

public interface IOutboxService
{
    Task<int> DispatchDueAsync(int batchSize = 25, CancellationToken cancellationToken = default);
}
