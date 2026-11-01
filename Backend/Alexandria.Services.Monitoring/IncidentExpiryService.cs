using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

/// <summary>
/// Closes incidents that have no producer able to resolve them: worker-cycle failures once the
/// worker stops failing, and user reports that no admin acted on.
/// </summary>
public class IncidentExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<IncidentExpiryOptions> options,
    ILogger<IncidentExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(options.Value.SweepIntervalMinutes, 1));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await SweepAsync(DateTime.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Incident expiry sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    internal async Task SweepAsync(DateTime now, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IOperationalEventService>();
        var config = options.Value;

        var cycleIdle = TimeSpan.FromMinutes(Math.Max(config.WorkerCycleFailureIdleMinutes, 1));
        var reportIdle = TimeSpan.FromHours(Math.Max(config.UserReportIdleHours, 1));

        var expiredCycles = await events.ExpireIdleIncidentsAsync(
            OperationalEventCode.WorkerCycleFailure, now - cycleIdle, ct);
        var expiredReports = await events.ExpireIdleIncidentsAsync(
            OperationalEventCode.UserReportedProblem, now - reportIdle, ct);

        if (expiredCycles + expiredReports > 0)
            logger.LogInformation(
                "Auto-expired {CycleCount} worker-cycle failures and {ReportCount} user reports",
                expiredCycles, expiredReports);
    }
}
