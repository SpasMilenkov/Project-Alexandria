using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.DeleteDir;

public class DeleteDirEndpoint(IDirectoryService dirService): Endpoint<DeleteDirRequest>
{
    public override void Configure()
    {
        Delete("/dir/{id}");
    }

    public override async Task HandleAsync(DeleteDirRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        await dirService.DeleteDirectoryAsync(req.Id, userId, req.Force, ct);
        await Send.OkAsync(cancellation: ct);
    }
}