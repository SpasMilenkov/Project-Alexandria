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

        foreach (var (name, entry) in report.Entries)
        {
            if (!TryResolveServiceType(name, out var serviceType)) continue;
            var code = ResolveCode(entry);

            if (entry.Status == HealthStatus.Healthy)
            {
                await ResolveIfActive(unitOfWork, serviceType, HealthcheckUnhealthy, ct);
                await ResolveIfActive(unitOfWork, serviceType, HealthcheckUnreachable, ct);
                continue;
            }

            var existing = await unitOfWork.OperationalEvents.GetActiveEventAsync(serviceType, code, ct);
            if (existing is not null) continue;

            await unitOfWork.OperationalEvents.AddAsync(new OperationalEvent
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
                        ServiceInstance = name,
                        CheckName = name,
                        Detail = entry.Exception?.Message ?? entry.Description
                    }),
                CreatedBy = SystemConfig.SystemId
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

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
            "worker-mediapreviews" => ServiceType.MediaPreviews,
            "worker-documentpreviews" => ServiceType.DocumentPreviews,
            "worker-transpilation" => ServiceType.Transpilation,
            "worker-lyrics" => ServiceType.Lyrics,
            "worker-mediametadata" => ServiceType.MediaMetadata,
            "postgres" or "storage" or "rabbitmq" => ServiceType.Api,
            _ => default
        };
        return serviceType != default || checkName is "postgres" or "storage" or "rabbitmq";
    }
}