using API.Features.Auth.Extensions;
using Common.Services;
using DTO.Files;
using FastEndpoints;

namespace API.Features.Storage.Files.SearchFile;

sealed class SearchFileEndpoint(IFileService fileService) : Endpoint<FileSearchQuery, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Post("/files/search");
    }

    public override async Task HandleAsync(FileSearchQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await fileService.SearchFile(req, userId, ct), ct);
    }
}