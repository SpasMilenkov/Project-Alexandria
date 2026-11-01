using Alexandria.Common.Services;
using Alexandria.Dto.Events.Operational;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Monitoring.GetAllUptimes;

public class GetAllUptimesRequest
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }
}

sealed class GetAllUptimesEndpoint(IOperationalEventService service)
    : Endpoint<GetAllUptimesRequest, List<ServiceUptime>>
{
    public override void Configure()
    {
        Get("/monitoring/uptime/all");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(GetAllUptimesRequest req, CancellationToken ct)
    {
        var result = await service.GetAllUptimesAsync(req.From.UtcDateTime, req.To.UtcDateTime, ct);
        await Send.OkAsync(result, ct);
    }
}
