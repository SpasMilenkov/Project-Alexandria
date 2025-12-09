using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.GetDirWithChildren;

public class GetDirWithChildrenEndpoint(IDirectoryService dirService)
    : Endpoint<GetDirWithChildrenRequest, GetDirWithChildrenResult>
{
    public override void Configure()
    {
        Get("dir-with-content/{id}");
    }

    public override async Task HandleAsync(GetDirWithChildrenRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        var dir = await dirService.GetDirectoryWithDetailsAsync(req.DirectoryId, userId, ct);
        await Send.OkAsync(new GetDirWithChildrenResult
        {
            Directory = dir
        }, ct);
    }
}