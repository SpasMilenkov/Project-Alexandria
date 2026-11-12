using Alexandria.Api.Features.Activity.Shared;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.GetEntityHistory;

sealed class GetEntityHistoryEndpoint(IAuditService auditService)
    : Endpoint<GetEntityHistoryRequest, PaginatedResult<AuditLogResult>>
{
    public override void Configure()
    {
        Get("/activity/entity/{EntityType}/{EntityId}");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s =>
        {
            s.Summary = "History of one entity, including its versions, previews, tags and share links";
            s.Description = "Non-admins only see rows they made or that affected their own data.";
        });
    }

    public override async Task HandleAsync(GetEntityHistoryRequest req, CancellationToken ct)
    {
        var page = new AuditPageRequest { Page = req.Page, PageSize = req.PageSize };

        var result = await auditService.GetEntityHistoryAsync(
            User.ToAuditViewer(), req.EntityType, req.EntityId, page, ct);

        await Send.OkAsync(result, ct);
    }
}
