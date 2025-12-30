using System.Security.Claims;
using Common.Services;
using DTO.Files;
using DTO.Search;
using FastEndpoints;
using Models.Enumerators;

namespace API.Features.Storage.Files.GetFilesByDirectoryId;

sealed class GetFilesByDirectoryIdRequest
{
    public Guid DirectoryId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }
}


sealed class GetFilesByDirectoryIdEndpoint(IStorageService storageService) : Endpoint<GetFilesByDirectoryIdRequest, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("/files/directory/{directoryId}");
    }

    public override async Task HandleAsync(GetFilesByDirectoryIdRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);
        var result = await storageService.GetFilesByDirectoryId(req.DirectoryId, userId, req.Page, req.PageSize,
            req.SortBy, req.SortDirection, ct);
        await Send.OkAsync(result, ct);
    }
}