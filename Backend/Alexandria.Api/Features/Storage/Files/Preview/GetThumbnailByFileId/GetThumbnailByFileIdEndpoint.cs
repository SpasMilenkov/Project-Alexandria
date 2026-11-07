using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetThumbnailByFileId;

internal sealed class GetThumbnailByFileIdRequest
{
    public Guid Id { get; set; }
}

internal sealed class GetThumbnailByFileIdEndpoint(
    IFileService fileService,
    IPreviewArtifactService previewArtifacts)
    : Endpoint<GetThumbnailByFileIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}/thumbnail");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Redirects to the thumbnail of a file's current version";
            s.Description = "Resolves the file's current version and issues a redirect to the " +
                            "version-scoped thumbnail endpoint. Not cached, always resolves fresh.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetThumbnailByFileIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var versionId = await fileService.GetCurrentVersionIdAsync(req.Id, userId, ct);
        if (versionId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";

        // The version URL below is cached long-term, so a manual cover has to change it.
        var stamp = await previewArtifacts.GetCoverStampAsync(versionId.Value, ct);
        var query = stamp is null ? string.Empty : $"?v={stamp}";

        await Send.RedirectAsync(
            $"/api/files/{req.Id}/versions/{versionId}/thumbnail{query}",
            isPermanent: false);
    }
}