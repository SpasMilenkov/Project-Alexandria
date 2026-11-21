using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetContentMatches;

/// <summary>
/// Excerpts of a file's document text around a search's words, for the owner only. Trashed files
/// are included, because the search covers the trash too.
/// </summary>
internal sealed class GetContentMatchesEndpoint(IUnitOfWork unitOfWork)
    : Endpoint<GetContentMatchesRequest, ContentMatchesResponse>
{
    public override void Configure()
    {
        Get("/files/{fileId}/content-matches");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetContentMatchesRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var contentObjectId = await unitOfWork.Files.GetCurrentContentObjectIdAsync(req.FileId, userId, ct);
        if (contentObjectId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        ContentMatchesResponse matches;

        if (req.FirstMatchOnly)
            matches = await unitOfWork.DocumentText.GetFirstContentMatchAsync(contentObjectId.Value, req.Query, ct);
        else
            matches = await unitOfWork.DocumentText.GetContentMatchesAsync(contentObjectId.Value, req.Query, ct);

        await Send.OkAsync(matches, ct);
    }
}