using API.Features.Auth.Extensions;
using Common.Services;
using DTO;
using DTO.Audit;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Activity.GetActivity;


sealed class GetUserActivityEndpoint(IAuditService auditService) : Endpoint<AuditLogQuery, PaginatedResult<AuditLogResult>>
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
