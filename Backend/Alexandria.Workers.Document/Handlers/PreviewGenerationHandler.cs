using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents;

namespace Alexandria.Workers.Document.Handlers;

public class PreviewGenerationHandler(
    ILogger<PreviewGenerationHandler> logger,
    IStorageService storage,
    IPdfPreviewService pdfPreviewService,
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
            await unitOfWork.FileVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.DeletedAt == null, ct);
        if (version is null) throw new InvalidOperationException($"Version with that ID: {message} does not exist.");

        var contentHash = Convert.ToHexStringLower(version.ContentHash);

        logger.LogInformation("Processing preview for version: {FileId}", message);

        var mimetype = await unitOfWork.Files.GetMimeTypeByVersionIdAsync(versionId, ct) ??
                       throw new InvalidOperationException("Mime type is missing");

        // Generate temp path with correct extension based on MIME type
        var extension = GetExtensionFromMimeType(mimetype);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        string? previewPath = null;
        string? thumbnailPath = null;

        try
        {
            await using (var tempFile = File.Create(tempPath))
            {
                await storage.StreamFile(versionId, tempFile, ct);
            }

            logger.LogInformation("File {FileId} downloaded to {TempPath}, size: {Size}",
                message, tempPath, new FileInfo(tempPath).Length);

            var fileCategory = storage.CategorizeFile(mimetype);
            var (generatedPreviewPath, generatedThumbnailPath) =
                await pdfPreviewService.GeneratePreviewAsync(tempPath, fileCategory, ct);

            previewPath = generatedPreviewPath;
            thumbnailPath = generatedThumbnailPath;

            if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
                throw new InvalidOperationException(
                    $"Preview generation failed. Expected path: {previewPath}, Exists: {File.Exists(previewPath)}");

            if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
                throw new InvalidOperationException(
                    $"Thumbnail generation failed. Expected path: {thumbnailPath}, Exists: {File.Exists(thumbnailPath)}");

            await using var previewStream = File.OpenRead(previewPath);
            logger.LogInformation("Preview generated at {PreviewPath}, size: {Size}",
                previewPath, previewStream.Length);

            await storage.UploadPreview($"previews/{contentHash}",
                "application/pdf",
                previewStream,
                versionId,
                SystemConfig.SystemId,
                previewStream.Length,
                PreviewKind.Preview,
                ct);

            await using var thumbnailStream = File.OpenRead(thumbnailPath);
            logger.LogInformation("Thumbnail generated at {ThumbnailPath}, size: {Size}",
                thumbnailPath, thumbnailStream.Length);

            await storage.UploadPreview($"thumbnails/{contentHash}",
                "image/png",
                thumbnailStream,
                versionId,
                SystemConfig.SystemId,
                thumbnailStream.Length,
                PreviewKind.Thumbnail,
                ct);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            if (previewPath != null && File.Exists(previewPath))
                File.Delete(previewPath);
            if (thumbnailPath != null && File.Exists(thumbnailPath))
                File.Delete(thumbnailPath);
        }
    }

    //TODO: I can probably unify those so that they are in one place with the media one maybe
    /// <summary>
    ///     Maps MIME types to their corresponding file extensions
    ///     Returns appropriate extension for preview-supported formats
    /// </summary>
    private static string GetExtensionFromMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return ".bin";

        return mimeType.ToLowerInvariant() switch
        {
            // Documents
            "application/msword" => ".doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.oasis.opendocument.text" => ".odt",
            "application/rtf" => ".rtf",
            "text/plain" => ".txt",

            // Spreadsheets
            "application/vnd.ms-excel" => ".xls",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.oasis.opendocument.spreadsheet" => ".ods",

            // Presentations
            "application/vnd.ms-powerpoint" => ".ppt",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
            "application/vnd.oasis.opendocument.presentation" => ".odp",

            // PDF
            "application/pdf" => ".pdf",

            // Images (if you ever want to process these)
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/svg+xml" => ".svg",

            // Text formats
            "text/markdown" => ".md",
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/html" => ".html",

            // Fallback for unknown types
            _ => ".bin"
        };
    }
}