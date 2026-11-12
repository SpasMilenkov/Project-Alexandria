using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.PlanActivityExport;

sealed class PlanActivityExportEndpoint(IAuditService auditService)
    : Endpoint<ActivityExportPlanRequest, AuditExportPlan>
{
    public override void Configure()
    {
        Get("/activity/export/plan");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "How the caller's activity export splits into files, with the bounds of each part");
    }

    public override async Task HandleAsync(ActivityExportPlanRequest req, CancellationToken ct)
    {
        // Same scope as the export itself, so the plan never counts rows a part could not return.
        var viewer = User.ToAuditViewer() with { IsAdmin = false };

        await Send.OkAsync(await auditService.PlanExportAsync(viewer, req.ToFilter(null, null), ct), ct);
    }
}
