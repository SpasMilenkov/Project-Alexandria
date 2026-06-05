using Alexandria.Common;
using Alexandria.Common.Services;

namespace Alexandria.Workers.Media.Handlers
{
    public class ImagePreviewGenerationHandler(
        ILogger<ImagePreviewGenerationHandler> logger,
        IStorageService storage,
        IFileService fileService,
        IImagePreviewService imagePreviewService,
        IUnitOfWork unitOfWork) : IPreviewGenerationHandler
    {
        public async Task HandleAsync(string message, CancellationToken ct = default)
        {
            var fileIdGuid = Guid.Parse(message);
            var fileData = await fileService.GetFileMetadataAsync(fileIdGuid, ct);
            if (fileData is null)
                throw new InvalidOperationException($"File {message} does not exist.");

            var fileHash = Convert.ToHexStringLower(
                await unitOfWork.Files.GetFileHashAsync(fileData.Id, fileData.OwnerId, ct)
                ?? throw new InvalidOperationException("File has no content object"));

            await using var fileStream = await storage.DownloadStreamableFile(fileIdGuid, fileData.OwnerId, ct);
            var previewStream = await imagePreviewService.GenerateImagePreviewAsync(fileStream, fileData.MimeType);

            await storage.UploadPreview(
                $"previews/{fileHash}",
                fileData.MimeType,
                previewStream,
                originalFileId: fileData.Id,
                uploadedBy: Guid.Empty, ct: ct);

            await fileService.UpdateFileMetadataAsync(fileIdGuid, Guid.Empty, hasPreview: true, ct: ct);

            logger.LogInformation("Image preview generated for file {FileId}", message);
        }
    }
}