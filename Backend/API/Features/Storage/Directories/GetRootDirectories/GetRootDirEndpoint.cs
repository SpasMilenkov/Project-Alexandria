using API.Features.Auth.Extensions;
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
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(PaginationParams req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var content = await directoryService.GetRootDirectoriesAsync(userId, req.Page, req.PageSize, req.SortDirection,
            req.SortBy, ct: ct);
        await Send.OkAsync(content, ct);
    }
}