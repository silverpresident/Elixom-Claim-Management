using ElixomClaim.Lib.Services;
namespace ElixomClaim.Web.HostedServices;
public sealed class SalaryGenerationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes; private readonly ILogger<SalaryGenerationHostedService> _logger;
    public SalaryGenerationHostedService(IServiceScopeFactory scopes, ILogger<SalaryGenerationHostedService> logger) { _scopes = scopes; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = _scopes.CreateScope(); var count = await scope.ServiceProvider.GetRequiredService<ISalaryPayrollService>().GenerateDueAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken); if (count > 0) _logger.LogInformation("Generated {Count} due payrolls.", count); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Salary generation iteration failed."); }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
