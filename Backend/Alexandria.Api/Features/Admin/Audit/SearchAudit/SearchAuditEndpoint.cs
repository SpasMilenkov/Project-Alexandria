using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Audit.SearchAudit;

sealed class SearchAuditEndpoint(IAuditService auditService)
    : Endpoint<AdminAuditQuery, PaginatedResult<AuditLogResult>>
{
    public override void Configure()
    {
        Get("/admin/audit");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Search the audit log across all users");
    }

    public override async Task HandleAsync(AdminAuditQuery req, CancellationToken ct)
    {
        var page = new AuditPageRequest
        {
            Page = req.Page,
            PageSize = req.PageSize,
            SortBy = req.SortBy,
            SortDirection = req.SortDirection,
        };

        var result = await auditService.SearchAsync(User.ToAuditViewer(), req.ToFilter(req.UserId, req.OwnerId),
            page, ct);
        await Send.OkAsync(result, ct);
    }
}
