using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.SearchFile;

internal sealed class SearchFileEndpoint(IFileService fileService)
    : Endpoint<FileSearchQuery, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("/files/search");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(FileSearchQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await fileService.SearchFile(req, userId, ct), ct);
    }
}