using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using WebMultipartSection = Microsoft.AspNetCore.WebUtilities.MultipartSection;

namespace Alexandria.Api.Features.Storage.Files.Preview.UploadVersionCover;

internal sealed class UploadVersionCoverResponse
{
    public required DateTime CoverUpdatedAt { get; set; }
}

internal sealed class UploadVersionCoverEndpoint(IPreviewArtifactService previewArtifacts)
    : EndpointWithoutRequest<UploadVersionCoverResponse>
{
    public override void Configure()
    {
        Post("/files/{id}/versions/{versionId}/cover");
        Description(b => b.WithTags("Preview", "Files"));
        Summary(s =>
        {
            s.Summary = "Sets an uploaded image as the cover of one audio file version";
            s.Description = "Multipart with a single image file (JPEG, PNG, GIF or WebP, up to 4 MB). " +
                            "The cover is kept when the version's previews are regenerated.";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fileId = Route<Guid>("id");
        var versionId = Route<Guid>("versionId");

        var boundary = TryGetBoundary();
        if (boundary is null)
        {
            await FailAsync("Request must be multipart with a boundary.", ct);
            return;
        }

        var image = await FindFileSectionAsync(boundary, ct);
        if (image is null)
        {
            await FailAsync("An image file is required.", ct);
            return;
        }

        try
        {
            // The section body is read directly, so the size cap stops an oversized upload
            // early instead of buffering it first.
            var coverUpdatedAt = await previewArtifacts.SetCustomCoverAsync(
                fileId, versionId, User.GetUserId(), image.Body, ct);

            await Send.OkAsync(new UploadVersionCoverResponse { CoverUpdatedAt = coverUpdatedAt }, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await FailAsync(ex.Message, ct);
        }
    }

    private string? TryGetBoundary()
    {
        var contentType = HttpContext.Request.ContentType;

        if (string.IsNullOrEmpty(contentType)
            || !contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value;

        return string.IsNullOrWhiteSpace(boundary) ? null : boundary;
    }

    private async Task<WebMultipartSection?> FindFileSectionAsync(string boundary, CancellationToken ct)
    {
        var reader = new MultipartReader(boundary, HttpContext.Request.Body);

        WebMultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
        {
            if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                && disposition.IsFileDisposition())
            {
                return section;
            }
        }

        return null;
    }

    private async Task FailAsync(string message, CancellationToken ct)
    {
        AddError(message);
        await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
    }
}
