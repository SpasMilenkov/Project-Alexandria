using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Activity.GetActivity;

sealed class GetUserActivityEndpoint(IAuditService auditService)
    : Endpoint<AuditLogQuery, PaginatedResult<AuditLogResult>>
{
    public override void Configure()
    {
        Get($"activity/user");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(AuditLogQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await auditService.GetLogs(req, userId, ct), ct);
    }
}