using System.Security.Claims;
using Common.Services;
using DTO.Directories;
using DTO.Files;
using FastEndpoints;
using Models.Enumerators;

namespace API.Features.Storage.Directories.GetSubdirectories;

internal sealed class GetSubdirectoriesRequest
{
    public Guid DirectoryId { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; } 
    public SortDirection SortDirection { get; set; }
    public DirectorySortBy SortBy { get; set; }
}

internal sealed class GetSubdirectories(IDirectoryService directoryService) : Endpoint<GetSubdirectoriesRequest, PaginatedResult<DirectorySummaryDto>>
{
    public override void Configure()
    {
        Get("/directories/sub");
        Description(x => x.WithTags("Directories"));

    }

    public override async Task HandleAsync(GetSubdirectoriesRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);
        var result = await directoryService.GetPaginatedDirectories(req.DirectoryId,
            userId, req.CurrentPage, req.PageSize, req.SortDirection, req.SortBy, ct);

        await Send.OkAsync(result, ct);
    }
}