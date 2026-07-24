using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Services.Preview.Media;

namespace Alexandria.Workers.Media.Handlers;

public class MediaPreviewGenerationHandler(
    ILogger<MediaPreviewGenerationHandler> logger,
    IStorageService storage,
    IMediaPreviewService mediaPreviewService,
    IUnitOfWork unitOfWork) : IPreviewGenerationHandler
{
    public async Task HandleAsync(string message, CancellationToken ct = default)
    {
        var versionId = Guid.Parse(message);
        var version =
            await unitOfWork.FileVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.DeletedAt == null, ct);

        if (version is null) throw new InvalidOperationException($"Version with that ID: {message} does not exist.");

        var contentHash = Convert.ToHexStringLower(version.ContentHash);

        logger.LogInformation("Processing preview for version: {FileId}", message);

        var mimetype = await unitOfWork.Files.GetMimeTypeByVersionIdAsync(versionId, ct) ??
                       throw new InvalidOperationException("Mime type is missing");

        logger.LogInformation("Processing media preview for file: {FileId}", message);

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

            logger.LogInformation("Media file {FileId} downloaded to {TempPath}, size: {Size}",
                message, tempPath, new FileInfo(tempPath).Length);

            var fileCategory = storage.CategorizeFile(mimetype);

            // Generate media previews (thumbnail + preview clip)
            var result = await mediaPreviewService.GeneratePreviewAsync(tempPath, fileCategory, ct);

            if (result is not { PreviewPath: { Length: > 0 }, ThumbnailPath: { Length: > 0 }, Metadata: not null })
            {
                throw new InvalidOperationException("Media preview generation failed");
            }

            previewPath = result.PreviewPath;
            thumbnailPath = result.ThumbnailPath;

            var previewSize = new FileInfo(previewPath).Length;
            var thumbnailSize = new FileInfo(thumbnailPath).Length;

            logger.LogInformation("Preview generated at {PreviewPath}, size: {Size}",
                result.PreviewPath, previewSize);

            await using var previewStream = File.OpenRead(previewPath);
            await using var thumbnailStream = File.OpenRead(thumbnailPath);
            await storage.UploadMediaData(previewStream, thumbnailStream, previewSize, thumbnailSize, contentHash,
                versionId, result.Metadata, ct);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            if (previewPath != null && File.Exists(previewPath)) File.Delete(previewPath);
            if (thumbnailPath != null && File.Exists(thumbnailPath)) File.Delete(thumbnailPath);
        }
    }


    /// <summary>
    /// Maps MIME types to their corresponding file extensions for media files
    /// </summary>
    private static string GetExtensionFromMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return ".bin";

        return mimeType.ToLowerInvariant() switch
        {
            // Video formats
            "video/mp4" => ".mp4",
            "video/x-msvideo" or "video/avi" => ".avi",
            "video/x-matroska" or "video/mkv" => ".mkv",
            "video/quicktime" => ".mov",
            "video/x-ms-wmv" => ".wmv",
            "video/webm" => ".webm",
            "video/x-flv" => ".flv",
            "video/3gpp" => ".3gp",
            "video/mpeg" => ".mpeg",

            // Audio formats
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/flac" => ".flac",
            "audio/aac" => ".aac",
            "audio/x-m4a" or "audio/mp4" => ".m4a",
            "audio/webm" => ".webm",
            "audio/wma" => ".wma",

            // Fallback
            _ => ".bin"
        };
    }
}