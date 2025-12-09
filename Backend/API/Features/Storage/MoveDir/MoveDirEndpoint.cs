using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.MoveDir;

public class MoveDirEndpoint(IDirectoryService dirService) : Endpoint<MoveDirRequest>
{
    public override void Configure()
    {
        Put("/move");
        //TODO: Remove later
        AllowAnonymous();
    }

    public override async Task HandleAsync(MoveDirRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        var dir = await dirService.MoveDirectoryAsync(req.DirectoryId, req.DestinationId, userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}