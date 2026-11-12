using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.GetCorrelatedActivity;

sealed class GetCorrelatedActivityEndpoint(IAuditService auditService)
    : Endpoint<GetCorrelatedActivityRequest, IReadOnlyList<AuditLogResult>>
{
    public override void Configure()
    {
        Get("/activity/correlation/{CorrelationId}");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Every row written by one request, oldest first");
    }

    public override async Task HandleAsync(GetCorrelatedActivityRequest req, CancellationToken ct)
    {
        var rows = await auditService.GetCorrelatedAsync(User.ToAuditViewer(), req.CorrelationId, ct);
        await Send.OkAsync(rows, ct);
    }
}
