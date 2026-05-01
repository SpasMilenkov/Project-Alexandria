using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Services.Preview.Documents;

namespace Alexandria.Workers.Document.Handlers;

public class PreviewGenerationHandler(
    ILogger<PreviewGenerationHandler> logger,
    IStorageService storage,
    IFileService fileService,
    IPdfPreviewService pdfPreviewService,
    IUnitOfWork unitOfWork) : IPreviewGenerationHandler
{
    public async Task HandleAsync(string message, CancellationToken ct = default)
    {
        var fileIdGuid = Guid.Parse(message);
        var fileData = await fileService.GetFileMetadataAsync(fileIdGuid, ct);
        if (fileData is null) throw new InvalidOperationException($"File with that ID: {message} does not exist.");
        var contentHash = Convert.ToHexStringLower(
            await unitOfWork.Files.GetFileHashAsync(fileData.Id, fileData.OwnerId, ct)
            ?? throw new InvalidOperationException("File does not have content object hash"));

        logger.LogInformation("Processing preview for file: {FileId}", message);

        // Generate temp path with correct extension based on MIME type
        var extension = GetExtensionFromMimeType(fileData.MimeType);
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");

        try
        {
            await using (var tempFile = File.Create(tempPath))
            {
                await storage.StreamFile(message, tempFile, ct);
            }

            logger.LogInformation("File {FileId} downloaded to {TempPath}, size: {Size}",
                message, tempPath, new FileInfo(tempPath).Length);

            var fileCategory = storage.CategorizeFile(fileData.MimeType);
            var previewPath = await pdfPreviewService.GeneratePreviewAsync(tempPath, fileCategory, ct);

            if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
            {
                throw new InvalidOperationException(
                    $"Preview generation failed. Expected path: {previewPath}, Exists: {File.Exists(previewPath)}");
            }

            logger.LogInformation("Preview generated at {PreviewPath}, size: {Size}",
                previewPath, new FileInfo(previewPath).Length);

            await using var previewStream = File.OpenRead(previewPath);

            await storage.UploadPreview($"previews/{contentHash}", "application/pdf",
                previewStream,
                originalFileId: fileData.Id, SystemConfig.SystemId, ct: ct);
            await fileService.UpdateFileMetadataAsync(fileIdGuid, SystemConfig.SystemId, hasPreview: true, ct: ct);

            File.Delete(previewPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }


    /// <summary>
    /// Maps MIME types to their corresponding file extensions
    /// Returns appropriate extension for preview-supported formats
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