using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Directories.SearchDirectory;

public class SearchDirectoryEndpoint(IDirectoryService directoryService)
    : Endpoint<SearchDirectoryRequest, PaginatedResult<DirectorySummaryDto>>
{
    public override void Configure()
    {
        Get("directories/search");
        Description(x => x.WithTags("Directories"));
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(SearchDirectoryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var query = new DirectorySearchQuery
        {
            DirectoryId = req.DirectoryId,
            ParentDirectoryId = req.ParentDirectoryId,

            NameContains = req.NameContains,

            OwnerId = req.OwnerId,
            IsShared = req.IsShared,

            CreatedAfter = req.CreatedAfter?.ToUniversalTime(),
            CreatedBefore = req.CreatedBefore?.ToUniversalTime(),
            UpdatedAfter = req.UpdatedAfter?.ToUniversalTime(),
            UpdatedBefore = req.UpdatedBefore?.ToUniversalTime(),
            DeletedBefore = req.DeletedBefore?.ToUniversalTime(),
            DeletedAfter = req.DeletedAfter?.ToUniversalTime(),

            HasFiles = req.HasFiles,
            HasSubdirectories = req.HasSubdirectories,

            IsDeleted = req.IsDeleted,
            IsStarred = req.IsStarred,

            CurrentPage = req.Page,
            PageSize = req.PageSize,
            SortBy = req.SortBy,
            SortDirection = req.SortDirection
        };

        var result = await directoryService.FindDirectoryAsync(userId, query, ct);
        await Send.OkAsync(result, ct);
    }
}