using Alexandria.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

public class JobRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<JobRecoveryOptions> options,
    ILogger<JobRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(options.Value.SweepIntervalMinutes, 1));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var recovery = scope.ServiceProvider.GetRequiredService<JobRecoveryService>();
                await recovery.SweepAsync(DateTime.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job recovery sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
