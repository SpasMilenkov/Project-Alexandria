using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;

namespace Alexandria.Common.Services;

public interface IStorageService
{
    public Task UploadPreview(string objectName,
        string contentType,
        Stream fileStream,
        Guid originalFileId,
        Guid uploadedBy,
        long contentLength = -1L,
        string? originalFileName = null,
        CancellationToken ct = default);

    Task UploadMediaData(Stream previewStream, Stream thumbnailStream, string objectName, Guid fileId,
        MediaMetadataDto metadataDto,
        CancellationToken ct = default);

    // File Download
    Task<Stream> DownloadFile(Guid fileId, Guid ownerId, CancellationToken ct);
    Task<Stream> DownloadStreamableFile(Guid fileId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Downloads the raw content object to a local file path.
    /// Transparently resolves whether the object lives in the promoted upload bucket
    /// or the temporary bucket based on <see cref="ContentObject.IsPromoted"/>.
    /// </summary>
    /// <param name="contentObjectId">The content object to download.</param>
    /// <param name="localFilePath">Absolute path of the file to write. The file is created or overwritten.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DownloadContentObjectAsync(Guid contentObjectId, string localFilePath, CancellationToken ct = default);

    /// <summary>
    /// Uploads every file under <paramref name="localDirectory"/> (recursively) to the streaming
    /// bucket, preserving relative paths under <paramref name="keyPrefix"/>.
    /// Example: <c>{localDirectory}/hls/seg001.ts</c> → <c>{keyPrefix}/hls/seg001.ts</c>.
    /// </summary>
    /// <param name="localDirectory">Root of the local output tree to upload.</param>
    /// <param name="keyPrefix">Prefix applied to every key in the streaming bucket.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UploadStreamingOutputAsync(string localDirectory, string keyPrefix, CancellationToken ct = default);

    /// <summary>
    /// Deletes all the old transpilation 
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task DeleteStreamingOutputByPrefixAsync(string prefix, CancellationToken ct = default);

    Task<string> GetStreamManifest(Guid versionId, Guid userId, CancellationToken ct = default);
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

    Task<string> GetPlaylistCoverUploadUrlAsync(Guid playlistId, Guid userId, string contentType,
        CancellationToken ct = default);

    Task<string> GetPlaylistCoverUrlAsync(Guid playlistId, Guid userId, CancellationToken ct = default);
}