using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Alexandria.Data.Models.Enumerators.Monitoring.OperationalEventCode;

namespace Alexandria.Services.Monitoring;

public class OperationalEventHealthPublisher(IServiceScopeFactory scopeFactory) : IHealthCheckPublisher
{
#pragma warning disable S927
    public async Task PublishAsync(HealthReport report, CancellationToken ct)
#pragma warning restore S927
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Several checks can share a service (postgres/storage/rabbitmq all report as Api),
        // so state is decided per service: a healthy sibling must not resolve a failing one.
        var byService = report.Entries
            .Select(e => (Name: e.Key, Entry: e.Value, Known: TryResolveServiceType(e.Key, out var type), Type: type))
            .Where(e => e.Known)
            .GroupBy(e => e.Type);

        foreach (var group in byService)
        {
            var serviceType = group.Key;
            var failing = group.Where(e => e.Entry.Status != HealthStatus.Healthy).ToList();

            if (failing.Count == 0)
            {
                await ResolveIfActive(unitOfWork, serviceType, HealthcheckUnhealthy, ct);
                await ResolveIfActive(unitOfWork, serviceType, HealthcheckUnreachable, ct);
                continue;
            }

            var code = failing.Any(e => ResolveCode(e.Entry) == HealthcheckUnhealthy)
                ? HealthcheckUnhealthy
                : HealthcheckUnreachable;

            // Only one healthcheck incident per service may be open at a time, otherwise
            // flipping between unreachable and unhealthy leaves overlapping downtime.
            var otherCode = code == HealthcheckUnhealthy ? HealthcheckUnreachable : HealthcheckUnhealthy;
            await ResolveIfActive(unitOfWork, serviceType, otherCode, ct);

            var touched = await unitOfWork.OperationalEvents.TouchActiveEventAsync(
                serviceType, code, DateTime.UtcNow, ct);
            if (touched > 0) continue;

            var primary = failing.First(e => ResolveCode(e.Entry) == code);
            var names = string.Join(", ", failing.Select(e => e.Name));

            await unitOfWork.OperationalEvents.AddActiveIfAbsentAsync(new OperationalEvent
            {
                Id = Guid.NewGuid(),
                ServiceType = serviceType,
                Code = code,
                Severity = OperationalEventSeverity.Failure,
                Status = OperationalEventStatus.Active,
                MetadataJson = OperationalEventExtensions.SerializeMetadata(
                    code,
                    new HealthcheckFailureMetadata
                    {
                        ServiceInstance = names,
                        CheckName = primary.Name,
                        Detail = DescribeFailures(failing.Select(e => (e.Name, e.Entry)).ToList())
                    }),
                CreatedBy = SystemConfig.SystemId
            }, ct);
        }
    }

    private static string? DescribeFailures(IReadOnlyList<(string Name, HealthReportEntry Entry)> failing)
    {
        if (failing.Count == 1)
            return Detail(failing[0].Entry);

        return string.Join("; ", failing.Select(f => $"{f.Name}: {Detail(f.Entry)}"));
    }

    private static string? Detail(HealthReportEntry entry) => entry.Exception?.Message ?? entry.Description;

    private static OperationalEventCode ResolveCode(HealthReportEntry entry)
    {
        if (entry.Tags.Contains("infrastructure"))
            return HealthcheckUnhealthy;

        var isUnreachable = entry.Exception is HttpRequestException or TaskCanceledException;
        return isUnreachable
            ? HealthcheckUnreachable
            : HealthcheckUnhealthy;
    }

    private static async Task ResolveIfActive(
        IUnitOfWork unitOfWork, ServiceType serviceType, OperationalEventCode code, CancellationToken ct)
    {
        var active = await unitOfWork.OperationalEvents.GetActiveEventAsync(serviceType, code, ct);
        if (active is not null)
            await unitOfWork.OperationalEvents.ResolveActiveEventsAsync(serviceType, code, DateTime.UtcNow, ct);
    }

    private static bool TryResolveServiceType(string checkName, out ServiceType serviceType)
    {
        serviceType = checkName switch
        {
            // Keeping this in sync with the appsettings is very important.
            //Mismatch between the actual config and the map breaks service monitoring
            // There is probably a smarter way to do this
            "worker-mediaworker" => ServiceType.MediaPreviews,
            "worker-documentworker" => ServiceType.DocumentPreviews,
            "worker-transpilationworker" => ServiceType.Transpilation,
            "worker-lyricsworker" => ServiceType.Lyrics,
            "worker-mediametadataworker" => ServiceType.MediaMetadata,
            "worker-playlistworker" => ServiceType.Playlist,
            "postgres" or "storage" or "rabbitmq" => ServiceType.Api,
            _ => default
        };
        return serviceType != default || checkName is "postgres" or "storage" or "rabbitmq";
    }
}