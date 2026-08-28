using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators.Monitoring;
using FastEndpoints;

namespace Alexandria.Api.Features.Monitoring.GetUptime;

public class GetUptimeRequest
{
    public required ServiceType Service { get; set; }
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }
}

sealed class GetUptimeEndpoint(IOperationalEventService service)
    : Endpoint<GetUptimeRequest, double>
{
    public override void Configure()
    {
        Get("/monitoring/uptime");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(60);
    }

    public override async Task HandleAsync(GetUptimeRequest req, CancellationToken ct)
    {
        var result = await service.GetUptimeAsync(req.Service, req.From.UtcDateTime, req.To.UtcDateTime, ct);
        await Send.OkAsync(result, ct);
    }
}