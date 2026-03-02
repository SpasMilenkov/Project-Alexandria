using System.Security.Claims;
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
    }

    public override async Task HandleAsync(AuditLogQuery req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);


        await Send.OkAsync(await auditService.GetLogs(req, userId, ct), ct);
    }
}