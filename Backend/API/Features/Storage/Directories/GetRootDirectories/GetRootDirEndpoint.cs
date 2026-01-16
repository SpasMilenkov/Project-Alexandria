using System.Security.Claims;
using Common.Services;
using DTO.Search;
using FastEndpoints;

namespace API.Features.Storage.Directories.GetRootDirectories;

public class GetRootDirEndpoint(IDirectoryService directoryService) : Endpoint<PaginationParams>
{
    public override void Configure()
    {
        Get("/directories/root");
        Description(x => x.WithTags("Directories"));
    }

    public override async Task HandleAsync(PaginationParams req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);
        var content = await directoryService.GetRootDirectoriesAsync(userId, req.Page, req.PageSize, req.SortDirection,
            req.SortBy, ct: ct);
        await Send.OkAsync(content, ct);
    }
}