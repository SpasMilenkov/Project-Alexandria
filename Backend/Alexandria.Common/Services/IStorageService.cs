using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;
using MediaMetadata = Alexandria.Dto.Files.MediaMetadata;

namespace Alexandria.Common.Services;

public interface IStorageService
{
    public Task<UploadResult> UploadPreview(
        string objectName,
        string contentType,
        Stream fileStream,
        Guid originalFileId,
        Guid uploadedBy,
        long contentLength = -1,
        string? originalFileName = null,
        CancellationToken ct = default);

    Task UploadMediaData(Stream previewStream, Stream thumbnailStream, string objectName, Guid fileId,
        MediaMetadata metadataDto,
        CancellationToken ct = default);

    // File Download
    Task<Stream> DownloadFile(Guid fileId, Guid ownerId, CancellationToken ct);
    Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct = default);
    Task<DownloadInfo> GetFileDownloadDetails(Guid fileId, Guid userId, CancellationToken ct = default);
    Task<DownloadInfo> GetFilVersioneDownloadDetails(Guid versionId, Guid userId, CancellationToken ct = default);

    Task StreamFile(
        string fileId,
        Stream destination,
        CancellationToken ct);

    Task<PreviewResultDto?> GetCachedPreview(Guid id, CancellationToken ct);

    FileCategory CategorizeFile(string mimeType);

    Task<string> GetFilePresignedUrl(Guid fileId, byte[] hash, string fileName, TimeSpan expiry);

    Task<UploadResult> FinalizeFileUpload(
        string objectName,
        Guid uploadId,
        Guid uploadedBy,
        byte[]? encryptionIv,
        byte[]? encryptionSalt,
        byte[]? integrityTag,
        string? encryptionHint,
        int? iterationCount,
        bool isEncrypted = false,
        Guid? directoryId = null,
        CancellationToken ct = default
    );

    Task<(Guid, string)> InitiateFileUpload(
        string contentType,
        string clientHash,
        Guid userId,
        long contentLength,
        Guid? directoryId = null,
        CancellationToken ct = default
    );

    Task<StorageBreakdown> GetStorageBreakdown(Guid userId, CancellationToken ct = default);
    Task<string> GenerateBackgroundImageGetUrl(string objectKey, TimeSpan expiry);
    Task DeleteBackgroundImageAsync(string objectKey, CancellationToken ct = default);
    Task<string> GenerateImageUploadUrl(string objectKey, TimeSpan expiry);
    Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default);

    Task StreamBulkZipAsync(
        Guid[] directoryIds,
        Guid[] fileIds,
        Guid userId,
        Stream destination,
        CancellationToken ct = default);
}