using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using FastEndpoints;

namespace Alexandria.Api.Features.Monitoring.ReportProblem;

public class ReportProblemRequest
{
    public required string Description { get; set; }
    public string? PageContext { get; set; }
}

sealed class ReportProblemEndpoint(IOperationalEventService service)
    : Endpoint<ReportProblemRequest, OperationalEvent>
{
    public override void Configure()
    {
        Post("/monitoring/report");
        Policies(Common.Auth.Policies.RequireUser);
        Throttle(hitLimit: 5, durationSeconds: 3600);
    }

    public override async Task HandleAsync(ReportProblemRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId(); // however your auth extracts this elsewhere
        var evt = await service.ReportProblemAsync(userId, req.Description, req.PageContext, ct);
        await Send.OkAsync(evt, ct);
    }
}