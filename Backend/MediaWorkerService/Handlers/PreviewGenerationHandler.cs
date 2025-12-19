using Common.Services;
using Models.Enumerators;
using PreviewService.Documents;
using PreviewService.Media;

namespace MediaWorkerService.Handlers;

public class PreviewGenerationHandler(
    ILogger<PreviewGenerationHandler> logger, 
    IStorageService storage, 
    IMediaPreviewService mediaPreviewService) : IPreviewGenerationHandler
{
    public async Task HandleAsync(string fileId, CancellationToken ct = default)
    {
        var fileIdGuid = Guid.Parse(fileId);
        var fileData = await storage.GetFileMetadata(fileIdGuid, ct);
        if (fileData is null) 
            throw new InvalidOperationException($"File with that ID: {fileId} does not exist.");
    
        logger.LogInformation("Processing media preview for file: {FileId}", fileId);
    
        // Generate temp path with correct extension based on MIME type
        var extension = GetExtensionFromMimeType(fileData.MimeType);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
    
        try
        {
            await using (var tempFile = File.Create(tempPath))
            {
                await storage.StreamFile(fileId, tempFile, ct);
            }
        
            logger.LogInformation("Media file {FileId} downloaded to {TempPath}, size: {Size}", 
                fileId, tempPath, new FileInfo(tempPath).Length);
        
            var fileCategory = storage.CategorizeFile(fileData.MimeType);
            
            // Generate media previews (thumbnail + preview clip)
            var result = await mediaPreviewService.GeneratePreviewAsync(tempPath, fileCategory, ct);
        
            if (result is not { PreviewPath: { Length: > 0 }, ThumbnailPath: { Length: > 0 }, Metadata: not null })
            {
                throw new InvalidOperationException("Media preview generation failed");
            }

            logger.LogInformation("Preview generated at {PreviewPath}, size: {Size}", 
                result.PreviewPath, new FileInfo(result.PreviewPath).Length);

            await using var previewStream = File.OpenRead(result.PreviewPath);
            await using var thumbnailStream = File.OpenRead(result.ThumbnailPath);
            await storage.UploadMediaData(previewStream, thumbnailStream, fileData.Name, fileData.Id, result.Metadata, ct);
            
            //TODO: Change Guid.Empty when the system account is seeded into the database
            await storage.UpdateFileMetadata(fileIdGuid, Guid.Empty, hasPreview: true, ct: ct);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }


    /// <summary>
    /// Maps MIME types to their corresponding file extensions for media files
    /// </summary>
    private string GetExtensionFromMimeType(string mimeType)
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