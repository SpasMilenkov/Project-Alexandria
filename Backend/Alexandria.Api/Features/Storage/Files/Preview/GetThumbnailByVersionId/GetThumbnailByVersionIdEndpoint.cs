using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetThumbnailByVersionId;

internal sealed class GetThumbnailByVersionIdRequest
{
    public Guid VersionId { get; set; }
}

internal sealed class GetThumbnailByVersionIdEndpoint(IPreviewService previewService)
    : Endpoint<GetThumbnailByVersionIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}/versions/{versionId}/thumbnail");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Redirects to the nginx-served thumbnail path for a specific file version";
            s.Description = "Thumbnail existence is checked against the Preview table keyed by " +
                            "(VersionId, Kind=Thumbnail); triggers generation if missing. The " +
                            "redirect target is a stable, content-addressed path served by " +
                            "nginx's auth_request-gated, cached previews location.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetThumbnailByVersionIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var thumbnailPath = await previewService.GetThumbnailAsync(req.VersionId, userId, ct);
        if (thumbnailPath is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        await Send.RedirectAsync(thumbnailPath, isPermanent: false);
    }
}