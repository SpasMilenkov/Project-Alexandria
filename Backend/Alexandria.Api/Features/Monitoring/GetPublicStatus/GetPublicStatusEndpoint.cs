using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using FastEndpoints;
using static Alexandria.Data.Models.Enumerators.Monitoring.OperationalEventSeverity;

namespace Alexandria.Api.Features.Monitoring.GetPublicStatus;

public record PublicServiceStatus(ServiceType ServiceType, string Status);

sealed class GetPublicStatusEndpoint(IOperationalEventService service)
    : EndpointWithoutRequest<List<PublicServiceStatus>>
{
    public override void Configure()
    {
        Get("/status");
        AllowAnonymous();
        ResponseCache(30);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var current = await service.GetCurrentStatusAsync(ct);

        var result = Enum.GetValues<ServiceType>()
            .Select(st =>
            {
                var active = current.FirstOrDefault(e => e.ServiceType == st);
                var status = active switch
                {
                    null => "Healthy",
                    { Severity: Failure } => "Down",
                    _ => "Degraded"
                };
                return new PublicServiceStatus(st, status);
            })
            .ToList();

        await Send.OkAsync(result, ct);
    }
}