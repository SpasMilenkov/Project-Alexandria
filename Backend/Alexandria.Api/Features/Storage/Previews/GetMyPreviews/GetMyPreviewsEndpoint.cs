using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Previews.GetMyPreviews;

public class GetMyPreviewsEndpoint(IStorageService storageService)
    : Endpoint<GetMyPreviewsRequest, PaginatedResult<UserPreviewDto>>
{
    public override void Configure()
    {
        Get("/storage/my-previews");
        Description(x => x.WithTags("Storage"));
        Summary(s =>
        {
            s.Summary = "List the caller's preview artifacts over live files, paged";
            s.Description = "Newest first. Optional file and created-before filters.";
            s.Responses[200] = "Preview page";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetMyPreviewsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(
            await storageService.GetMyPreviewsAsync(
                userId, req.FileId, req.CreatedBefore?.UtcDateTime, req.Page, req.PageSize, ct),
            ct);
    }
}