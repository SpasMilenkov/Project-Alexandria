using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Policies;
using Alexandria.Common.Helpers;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.Policies;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.AutoTag;

public class AutoTagFileResponse
{
    public Guid FileId { get; set; }
    public bool Queued { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Manual trigger for a single file's auto-tag run. Refuses with a 400 when the feature flag
/// is off or the file is not audio, skips files that were already enriched successfully, and
/// otherwise publishes a <c>media-metadata.enrich</c> trigger (same path as the AutoTag policy
/// rule). The file id comes from the route — no request body is expected (bodyless POST, like
/// <c>/auth/logout</c>).
/// </summary>
public class AutoTagFileEndpoint(
    IPublisherService publisher,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : EndpointWithoutRequest<AutoTagFileResponse>
{
    public override void Configure()
    {
        Post("/files/{FileId}/auto-tag");
        Summary(s =>
        {
            s.Summary = "Trigger auto-tagging for a file";
            s.Description =
                "Queues an enrichment run for the file so its auto-tags are (re)derived. Idempotent when the file already has a successful enrichment.";
            s.Responses[200] = "Enrichment trigger published or skipped as already enriched";
            s.Responses[400] = "Auto-tagging is disabled, or the file's MIME type is not audio";
            s.Responses[404] = "File not found";
        });
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var routeFileId = (string?)HttpContext.Request.RouteValues["FileId"];
        if (!Guid.TryParse(routeFileId, out var fileId))
        {
            ThrowError("File was not found.", 404);
            return;
        }

        var file = await unitOfWork.Files.GetFileWithOwnershipByIdAsync(fileId, userId, ct);
        if (file is null)
        {
            ThrowError($"File {fileId} was not found.", 404);
            return;
        }

        if (!AutoTagSupportedFileTypes.IsSupported(file.MimeType))
        {
            ThrowError("Auto-tagging supports audio files only.", 400);
            return;
        }

        var autoTaggingEnabled = bool.TryParse(configuration["Features:Autotagging"], out var enabled)
                                 && enabled;

        try
        {
            var queued = await AutoTagTrigger.QueueIfNeededAsync(
                publisher, unitOfWork, autoTaggingEnabled, fileId, file.MimeType, ct: ct);

            await Send.OkAsync(new AutoTagFileResponse
            {
                FileId = fileId,
                Queued = queued,
                Message = queued
                    ? "Auto-tag enrichment queued."
                    : "File already has a successful enrichment; skipped."
            }, ct);
        }
        catch (AutoTaggingDisabledException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}