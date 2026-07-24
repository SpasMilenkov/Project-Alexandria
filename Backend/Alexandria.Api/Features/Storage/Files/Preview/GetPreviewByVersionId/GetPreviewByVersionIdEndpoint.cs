using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetPreviewByVersionId;

internal sealed class GetPreviewByVersionIdRequest
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
}

internal sealed class GetPreviewByVersionIdEndpoint(IPreviewService previewService)
    : Endpoint<GetPreviewByVersionIdRequest, PreviewResultDto>
{
    public override void Configure()
    {
        Get("/files/{id}/versions/{versionId}/preview");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Endpoint for generating preview of files for use in the frontend";
            s.Description = "Autodetects filetype and generates appropriate preview with predefined sizes. " +
                            "If the preview exists it will be fetched from an already cached source, " +
                            "if there is no preview it will be generated on the fly, which may consume " +
                            "a larger amount of resources and take a while.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetPreviewByVersionIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var preview = await previewService.GetPreviewUrlAsync(req.VersionId, userId, ct);
        if (preview is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(preview, ct);
    }
}