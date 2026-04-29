using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Microsoft.Extensions.Options;

namespace Alexandria.Workers.Media.Handlers
{
    public class ImagePreviewGenerationHandler(
        ILogger<ImagePreviewGenerationHandler> logger,
        IStorageService storage,
        IFileService fileService,
        IImagePreviewService imagePreviewService,
        IOptions<S3Config> storageConfig,
        IUnitOfWork unitOfWork) : IPreviewGenerationHandler
    {
        public async Task HandleAsync(string fileId, CancellationToken ct = default)
        {
            var fileIdGuid = Guid.Parse(fileId);
            var fileData = await fileService.GetFileMetadata(fileIdGuid, ct);
            if (fileData is null)
                throw new InvalidOperationException($"File {fileId} does not exist.");

            var fileHash = Convert.ToHexStringLower(
                await unitOfWork.Files.GetFileHash(fileData.Id, fileData.OwnerId, ct)
                ?? throw new InvalidOperationException("File has no content object"));

            await using var fileStream = await storage.DownloadStreamableFile(fileIdGuid, fileData.OwnerId, ct);
            var previewStream = await imagePreviewService.GenerateImagePreview(fileStream, fileData.MimeType);

            await storage.UploadPreview(
                storageConfig.Value.PreviewBucket,
                $"previews/{fileHash}",
                fileData.MimeType,
                previewStream,
                uploadedBy: Guid.Empty,
                originalFileId: fileData.Id,
                ct: ct);

            await fileService.UpdateFileMetadata(fileIdGuid, Guid.Empty, hasPreview: true, ct: ct);

            logger.LogInformation("Image preview generated for file {FileId}", fileId);
        }
    }
}