using System.Security.Claims;
using Common.Services;
using DTO.Directories;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Storage.Directories.SearchDirectory;

public class SearchDirectoryEndpoint(IDirectoryService directoryService)
    : Endpoint<SearchDirectoryRequest, PaginatedResult<DirectorySummaryDto>>
{
    public override void Configure()
    {
        Get("directories/search");
        Description(x => x.WithTags("Directories"));

    }

    public override async Task HandleAsync(SearchDirectoryRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        
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
            DeletedAt = req.DeletedAt?.ToUniversalTime(),
    
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