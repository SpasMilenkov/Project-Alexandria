using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.GetRootDir;

public class GetRootDirEndpoint(IDirectoryService directoryService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/dir/root");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        var content = await directoryService.GetRootDirectoriesAsync(userId, ct);
        await Send.OkAsync(content, ct);
    }
}