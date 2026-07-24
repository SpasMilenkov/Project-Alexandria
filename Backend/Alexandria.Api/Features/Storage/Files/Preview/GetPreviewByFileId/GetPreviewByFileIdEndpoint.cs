using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetPreviewByFileId;

internal sealed class GetPreviewByFileIdRequest
{
    public Guid Id { get; set; }
}

internal sealed class GetPreviewByFileIdEndpoint(IFileService fileService)
    : Endpoint<GetPreviewByFileIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}/preview");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Redirects to the preview of a file's current version";
            s.Description = "Resolves the file's current version and issues a redirect to the " +
                            "version-scoped preview endpoint. Not cached, always resolves fresh.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetPreviewByFileIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var versionId = await fileService.GetCurrentVersionIdAsync(req.Id, userId, ct);
        if (versionId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "no-store";

        await Send.RedirectAsync(
            $"/api/files/{req.Id}/versions/{versionId}/preview",
            isPermanent: false);
    }
}