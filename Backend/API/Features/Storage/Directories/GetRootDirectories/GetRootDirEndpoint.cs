using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.GetRootDirectories;

public class GetRootDirEndpoint(IDirectoryService directoryService) : Endpoint<GetRootDirRequest>
{
    public override void Configure()
    {
        Get("/directories/root");
        Description(x => x.WithTags("Directories"));

    }

    public override async Task HandleAsync(GetRootDirRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        var content = await directoryService.GetRootDirectoriesAsync(userId, req.Page, req.PageSize, ct);
        await Send.OkAsync(content, ct);
    }
}