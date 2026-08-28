using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Monitoring.GetOperationalEvents;

public class GetOperationalEventsRequest
{
    public ServiceType? ServiceType { get; set; }
    public OperationalEventStatus? Status { get; set; }
    public OperationalEventSeverity? Severity { get; set; }
    public OperationalEventCode? Code { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

sealed class GetOperationalEventsEndpoint(IOperationalEventService service)
    : Endpoint<GetOperationalEventsRequest, PaginatedResult<OperationalEvent>>
{
    public override void Configure()
    {
        Get("/monitoring/events");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(GetOperationalEventsRequest req, CancellationToken ct)
    {
        var query = new OperationalEventQuery(
            req.ServiceType, req.Status, req.Severity, req.Code,
            req.From?.UtcDateTime, req.To?.UtcDateTime, req.Page, req.PageSize);

        var result = await service.GetEventsAsync(query, ct);
        await Send.OkAsync(result, ct);
    }
}