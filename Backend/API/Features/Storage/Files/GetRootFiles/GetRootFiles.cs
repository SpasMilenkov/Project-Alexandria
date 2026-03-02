using System.Security.Claims;
using API.Features.Auth.Extensions;
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
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(PaginationParams req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(
            await storageService.GetRootFilesAsync(userId, req.Page, req.PageSize, req.SortBy, req.SortDirection, ct),
            ct);
    }
}