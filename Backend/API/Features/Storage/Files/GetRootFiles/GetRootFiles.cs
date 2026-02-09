using System.Security.Claims;
using Common.Services;
using DTO.Files;
using DTO.Search;
using FastEndpoints;

namespace API.Features.Storage.Files.GetRootFiles;

sealed class GetRootFilesEndpoint(IFileService storageService) : Endpoint<PaginationParams, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("/files/root");
        Description(x => x.WithTags("Files"));
    }

    public override async Task HandleAsync(PaginationParams req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);

        await Send.OkAsync(
            await storageService.GetRootFilesAsync(userId, req.Page, req.PageSize, req.SortBy, req.SortDirection, ct),
            ct);
    }
}