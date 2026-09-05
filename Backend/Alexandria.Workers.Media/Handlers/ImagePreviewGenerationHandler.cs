using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Workers.Media.Handlers;

public class ImagePreviewGenerationHandler(
    ILogger<ImagePreviewGenerationHandler> logger,
    IStorageService storage,
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
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await PreviewJobLifecycle.FailAsync(unitOfWork, jobId, ex.Message, ct);
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

        var previewStream = await imagePreviewService.GenerateImagePreviewAsync(fileStream, mimetype, ct: ct);

        fileStream.Position = 0;

        var thumbnailStream = await imagePreviewService.GenerateImageThumbnailAsync(fileStream, mimetype, ct: ct);

        try
        {
            await storage.UploadPreview(
                $"previews/{fileHash}",
                mimetype,
                previewStream,
                versionId,
                contentLength: previewStream.Length,
                uploadedBy: SystemConfig.SystemId,
                kind: PreviewKind.Preview,
                ct: ct);

            await storage.UploadPreview(
                $"thumbnails/{fileHash}",
                mimetype,
                thumbnailStream,
                versionId,
                contentLength: thumbnailStream.Length,
                uploadedBy: SystemConfig.SystemId,
                kind: PreviewKind.Thumbnail,
                ct: ct);

            logger.LogInformation("Image preview and thumbnail generated for file {FileId}", message);
        }
        finally
        {
            await previewStream.DisposeAsync();
            await thumbnailStream.DisposeAsync();
        }
    }
}