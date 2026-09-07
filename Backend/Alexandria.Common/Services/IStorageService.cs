using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;
using Alexandria.Dto.Previews;

namespace Alexandria.Common.Services;

public interface IStorageService
{
    public Task UploadPreview(
        string objectName,
        string contentType,
        Stream fileStream,
        Guid versionId,
        Guid uploadedBy,
        long contentLength,
        PreviewKind kind,
        CancellationToken ct = default);

    Task UploadMediaData(Stream previewStream,
        Stream thumbnailStream,
        long previewSize,
        long thumbnailSize,
        string objectName,
        Guid versionId,
        MediaMetadataDto metadataDto,
        CancellationToken ct = default);

    // File Download
    Task<Stream> DownloadFile(Guid versionId, Guid userId, CancellationToken ct = default);
    Task<Stream> DownloadSeekableFile(Guid versionId, Guid userId, CancellationToken ct = default);
    Task<Stream> DownloadSeekableFile(Guid versionId, CancellationToken ct = default);

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
        Guid versionId,
        Stream destination,
        CancellationToken ct = default);

    Task<string?> GetCachedPreview(Guid versionId, PreviewKind kind, CancellationToken ct = default);

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

    /// <summary>
    /// A user's preview artifacts over live files, optionally scoped to one file and/or
    /// created before a cutoff. Newest first with an id tiebreak for stable paging.
    /// </summary>
    Task<PaginatedResult<UserPreviewDto>> GetMyPreviewsAsync(
        Guid userId, Guid? fileId, DateTime? createdBefore, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a single preview artifact (S3 object + row). Job and PreviewJob rows are
    /// untouched; the preview regenerates from the file. Returns freed bytes.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Preview or its owning file is missing.</exception>
    /// <exception cref="UnauthorizedAccessException">Not the owner and not an admin.</exception>
    Task<long> DeletePreviewAsync(Guid previewId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Deletes previews of one file, optionally only those created before a cutoff.
    /// S3 objects still referenced by other previews (shared content hash) are kept.
    /// </summary>
    /// <exception cref="KeyNotFoundException">File is missing.</exception>
    /// <exception cref="UnauthorizedAccessException">Not the owner and not an admin.</exception>
    Task<(int DeletedCount, long FreedBytes)> DeletePreviewsByFileAsync(
        Guid fileId, Guid userId, bool isAdmin, DateTime? createdBefore, CancellationToken ct = default);

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
}