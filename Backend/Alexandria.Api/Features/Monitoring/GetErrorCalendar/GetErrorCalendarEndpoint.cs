using Alexandria.Common.Services;
using Alexandria.Dto.Events.Operational;
using FastEndpoints;

namespace Alexandria.Api.Features.Monitoring.GetErrorCalendar;

public class GetErrorCalendarRequest
{
    public required DateTimeOffset From { get; set; }
    public required DateTimeOffset To { get; set; }
}

sealed class GetErrorCalendarEndpoint(IOperationalEventService service)
    : Endpoint<GetErrorCalendarRequest, List<ErrorAggregate>>
{
    public override void Configure()
    {
        Get("/monitoring/events/calendar");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(60);
    }

    public override async Task HandleAsync(GetErrorCalendarRequest req, CancellationToken ct)
    {
        var result = await service.GetErrorCountsAsync(req.From.UtcDateTime, req.To.UtcDateTime, ct);
        await Send.OkAsync(result, ct);
    }
}