using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Mapping;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Monitoring;

public class JobFailureThresholdChecker : BackgroundService
{
    private const double ThresholdRate = 0.15; // 15% failure rate
    private const int MinimumSampleSize = 10; // don't judge a rate off a handful of jobs
    private static readonly TimeSpan Window = TimeSpan.FromHours(2);

    private readonly IJobOutcomeTracker _outcomeTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceType _monitoredService;
    private readonly JobType _monitoredJobType;
    private readonly ILogger<JobFailureThresholdChecker> _logger;

    public JobFailureThresholdChecker(
        IJobOutcomeTracker outcomeTracker,
        IServiceScopeFactory scopeFactory,
        ServiceType monitoredService,
        ILogger<JobFailureThresholdChecker> logger)
    {
        _outcomeTracker = outcomeTracker;
        _scopeFactory = scopeFactory;
        _monitoredService = monitoredService;
        _logger = logger;

        // Api has no backing Job table, its status comes from the healthcheck
        // publisher writing OperationalEvent directly. Fail at startup rather
        // than silently no-op on every tick if this is ever misconfigured.
        _monitoredJobType = monitoredService.ToJobType()
                            ?? throw new ArgumentOutOfRangeException(
                                nameof(monitoredService),
                                monitoredService,
                                $"{monitoredService} has no backing Job table; it cannot be monitored by {nameof(JobFailureThresholdChecker)}.");
    }

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
                _logger.LogError(ex, "Failed to run job failure threshold check for {ServiceType}", _monitoredService);
            }
        }
    }

    internal async Task CheckAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var since = DateTime.UtcNow - Window;
        var (failures, total) = await _outcomeTracker.GetCountsSinceAsync(_monitoredJobType, since, ct);

        var existing = await unitOfWork.OperationalEvents.GetActiveEventAsync(
            _monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, ct);

        // guard against low-volume noise: 1 failure out of 1 dispatched job
        // is a 100% rate but not a meaningful signal on its own
        if (total < MinimumSampleSize)
        {
            if (existing is not null)
                await unitOfWork.OperationalEvents.ResolveActiveEventsAsync(
                    _monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, DateTime.UtcNow, ct);

            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var failureRate = (double)failures / total;

        if (failureRate > ThresholdRate && existing is null)
        {
            await unitOfWork.OperationalEvents.AddAsync(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                ServiceType = _monitoredService,
                Code = OperationalEventCode.ErrorRateThresholdExceeded,
                Severity = OperationalEventSeverity.DegradedPerformance,
                Status = OperationalEventStatus.Active,
                MetadataJson = OperationalEventExtensions.SerializeMetadata(
                    OperationalEventCode.ErrorRateThresholdExceeded,
                    new ErrorRateThresholdMetadata
                    {
                        ServiceInstance = _monitoredService.ToString(),
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
                _monitoredService, OperationalEventCode.ErrorRateThresholdExceeded, DateTime.UtcNow, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}