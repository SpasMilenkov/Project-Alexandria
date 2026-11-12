using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.ExportActivity;

sealed class ExportActivityEndpoint(IAuditService auditService) : Endpoint<ActivityExportRequest>
{
    public override void Configure()
    {
        Get("/activity/export");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Download the caller's activity as CSV or JSON, newest first");
    }

    public override async Task HandleAsync(ActivityExportRequest req, CancellationToken ct)
    {
        var viewer = User.ToAuditViewer();

        // Admins export other users' rows through /admin/audit/export; here everyone sees only
        // their own history.
        var rows = auditService.ExportAsync(viewer with { IsAdmin = false }, req.ToExportFilter(null, null), ct);

        await AuditExportWriter.WriteAsync(HttpContext.Response, rows, req.Format, "activity", ct);
    }
}
