using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Audit.ExportAudit;

sealed class ExportAuditEndpoint(IAuditService auditService) : Endpoint<AdminAuditExportRequest>
{
    public override void Configure()
    {
        Get("/admin/audit/export");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Download audit rows across all users as CSV or JSON, newest first");
    }

    public override async Task HandleAsync(AdminAuditExportRequest req, CancellationToken ct)
    {
        var rows = auditService.ExportAsync(User.ToAuditViewer(), req.ToExportFilter(req.UserId, req.OwnerId), ct);
        await AuditExportWriter.WriteAsync(HttpContext.Response, rows, req.Format, "audit", ct);
    }
}
