using System.Security.Claims;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Directories.GetDirectoryPath;

public class GetDirectoryPathEndpoint(IDirectoryService dirService): Endpoint<GetDirectoryPathRequest, GetDirectoryPathResponse>
{
    public override void Configure()
    {
        Get("directories/path/{id}");
        Description(x => x.WithTags("Directories"));

    }

    public override async Task HandleAsync(GetDirectoryPathRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);
        
        var pathParts = await dirService.GetDirectoryPathAsync(req.Id, userId, ct);

        await Send.OkAsync(new GetDirectoryPathResponse
        {
            PathParts = pathParts
        }, ct);
    }
}