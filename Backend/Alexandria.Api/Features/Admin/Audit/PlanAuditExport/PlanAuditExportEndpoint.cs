using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Audit.PlanAuditExport;

sealed class PlanAuditExportEndpoint(IAuditService auditService)
    : Endpoint<AdminAuditExportPlanRequest, AuditExportPlan>
{
    public override void Configure()
    {
        Get("/admin/audit/export/plan");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "How an audit export across all users splits into files, with the bounds of each part");
    }

    public override async Task HandleAsync(AdminAuditExportPlanRequest req, CancellationToken ct)
    {
        var filter = req.ToFilter(req.UserId, req.OwnerId);

        await Send.OkAsync(await auditService.PlanExportAsync(User.ToAuditViewer(), filter, ct), ct);
    }
}
