using ElixomClaim.Lib.Services;

namespace ElixomClaim.Web.HostedServices;

public class OutboxDispatchHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatchHostedService> _logger;
    public OutboxDispatchHostedService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatchHostedService> logger) { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var wakeUps = await scope.ServiceProvider.GetRequiredService<IOutboxWakeUpProcessor>().ProcessPendingAsync(stoppingToken);
                var count = await scope.ServiceProvider.GetRequiredService<IOutboxService>().DispatchDueAsync(cancellationToken: stoppingToken);
                if (count > 0 || wakeUps > 0) _logger.LogInformation("Processed {Count} due outbox emails and {WakeUpCount} wake-up request(s).", count, wakeUps);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Outbox dispatch iteration failed."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
