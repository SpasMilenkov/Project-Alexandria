using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Monitoring;

public class JobFailureThresholdChecker(
    IJobOutcomeTracker outcomeTracker,
    IServiceScopeFactory scopeFactory,
    ServiceType monitoredService,
    ILogger<JobFailureThresholdChecker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await CheckAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run job failure threshold check for {ServiceType}", monitoredService);
            }
        }
    }

    internal async Task CheckAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var since = DateTime.UtcNow - Window;
        var (failures, total) = outcomeTracker.GetCountsSince(monitoredService, since);

        var existing = await unitOfWork.OperationalEvents.GetActiveEventAsync(
            monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, ct);

        // guard against low-volume noise: 1 failure out of 1 dispatched job
        // is a 100% rate but not a meaningful signal on its own
        if (total < MinimumSampleSize)
        {
            if (existing is not null)
                await unitOfWork.OperationalEvents.ResolveActiveEventsAsync(
                    monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, DateTime.UtcNow, ct);

            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var failureRate = (double)failures / total;

        if (failureRate > ThresholdRate && existing is null)
        {
            await unitOfWork.OperationalEvents.AddAsync(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                ServiceType = monitoredService,
                Code = OperationalEventCode.ErrorRateThresholdExceeded,
                Severity = OperationalEventSeverity.DegradedPerformance,
                Status = OperationalEventStatus.Active,
                MetadataJson = OperationalEventExtensions.SerializeMetadata(
                    OperationalEventCode.ErrorRateThresholdExceeded,
                    new ErrorRateThresholdMetadata
                    {
                        ServiceInstance = monitoredService.ToString(),
                        WindowMinutes = (int)Window.TotalMinutes,
                        FailureCount = failures,
                        Threshold = (int)(ThresholdRate * 100)
                    }),
                CreatedBy = SystemConfig.SystemId
            }, ct);
        }
        else if (failureRate <= ThresholdRate && existing is not null)
        {
            await unitOfWork.OperationalEvents.ResolveActiveEventsAsync(
                monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, DateTime.UtcNow, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private const double ThresholdRate = 0.15; // 15% failure rate
    private const int MinimumSampleSize = 10; // don't judge a rate off a handful of jobs
}