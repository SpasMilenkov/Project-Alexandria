using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Activity.GetActivity;

sealed class GetUserActivityEndpoint(IAuditService auditService)
    : Endpoint<AuditLogQuery, PaginatedResult<AuditLogResult>>
{
    public override void Configure()
    {
        Get("activity/user");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(AuditLogQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await auditService.GetLogsAsync(userId, req, ct), ct);
    }
}
