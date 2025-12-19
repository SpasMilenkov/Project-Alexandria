using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.UpdateDir;

public class UpdateDirEndpoint(IDirectoryService dirService): Endpoint<UpdateDirRequest, UpdateDirResponse>
{
    public override void Configure()
    {
        Put("/directories");
        Description(x => x.WithTags("Directories"));

    }

    public override async Task HandleAsync(UpdateDirRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
    
        var dir = await dirService.UpdateDirectoryAsync(req.DirectoryId, req.Name, userId, ct);
        await Send.OkAsync(new UpdateDirResponse
        {
            Directory = dir
        });
    }
}