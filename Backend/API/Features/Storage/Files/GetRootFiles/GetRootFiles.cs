using System.Security.Claims;
using Common.Services;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Storage.Files.GetRootFiles;

sealed class GetRootFilesRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
}

sealed class GetRootFilesEndpoint(IStorageService storageService) : Endpoint<GetRootFilesRequest, PaginatedResult<FileSummary>>
{
    public override void Configure()
    {
        Get("/files/root");
        Description(x => x.WithTags("Files"));

    }

    public override async Task HandleAsync(GetRootFilesRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
    
        var userId = Guid.Parse(userIdString);

        await Send.OkAsync(await storageService.GetRootFilesAsync(userId, req.Page, req.PageSize, ct), ct);
    }
}