using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Workers.Media.Handlers;

public class ImagePreviewGenerationHandler(
    ILogger<ImagePreviewGenerationHandler> logger,
    IContentStorageService storage,
    IPreviewArtifactService artifacts,
    IImagePreviewService imagePreviewService,
    IUnitOfWork unitOfWork) : IPreviewGenerationHandler
{
    public async Task HandleAsync(string message, CancellationToken ct = default)
    {
        if (!Guid.TryParse(message, out var jobId))
            throw new InvalidOperationException($"Preview job message is not a valid job ID: {message}.");

        var previewJob = await PreviewJobLifecycle.TryClaimAsync(unitOfWork, jobId, ct);
        if (previewJob is null)
        {
            logger.LogInformation("Preview job {JobId} already claimed; skipping duplicate delivery.", jobId);
            return;
        }

        try
        {
            await GenerateAsync(previewJob.VersionId, ct);
            await PreviewJobLifecycle.CompleteAsync(unitOfWork, jobId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await PreviewJobLifecycle.ReleaseAsync(unitOfWork, jobId);
            throw;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await PreviewJobLifecycle.FailAsync(unitOfWork, jobId, ex, ct);
            throw;
        }
    }

    private async Task GenerateAsync(Guid versionId, CancellationToken ct)
    {
        var message = versionId.ToString();
        var version =
            await unitOfWork.FileVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Version with that ID: {message} does not exist.");

        var fileHash = Convert.ToHexStringLower(version.ContentHash);

        var mimetype = await unitOfWork.Files.GetMimeTypeByVersionIdAsync(versionId, ct) ??
                       throw new InvalidOperationException("Mime type is missing");

        await using var fileStream = await storage.DownloadSeekableFile(versionId, ct);
        await using var generated = await imagePreviewService.GenerateAsync(fileStream, mimetype, ct);

        await artifacts.UploadPreview(
            $"previews/{fileHash}",
            generated.Preview.ContentType,
            generated.Preview.Content,
            versionId,
            contentLength: generated.Preview.Content.Length,
            uploadedBy: SystemConfig.SystemId,
            kind: PreviewKind.Preview,
            ct: ct);

        await artifacts.UploadPreview(
            $"thumbnails/{fileHash}",
            generated.Thumbnail.ContentType,
            generated.Thumbnail.Content,
            versionId,
            contentLength: generated.Thumbnail.Content.Length,
            uploadedBy: SystemConfig.SystemId,
            kind: PreviewKind.Thumbnail,
            ct: ct);

        logger.LogInformation("Image preview and thumbnail generated for file {FileId}", message);
    }
}