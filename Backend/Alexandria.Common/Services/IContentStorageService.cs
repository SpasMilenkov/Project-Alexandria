using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Metrics;

namespace Alexandria.Common.Services;

/// <summary>
/// Owns content bytes (<c>UploadBucket</c>, <c>TempBucket</c>, <c>StreamingBucket</c>):
/// upload lifecycle, downloads, presigned URLs, bulk zip, streaming output, manifest,
/// storage breakdown.
/// </summary>
public interface IContentStorageService
{
    // File Download
    Task<Stream> DownloadFile(Guid versionId, Guid userId, CancellationToken ct = default);
    Task<Stream> DownloadSeekableFile(Guid versionId, Guid userId, CancellationToken ct = default);
    Task<Stream> DownloadSeekableFile(Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Downloads the raw content object to a local file path, first waiting for it to be promoted,
    /// because jobs can reach a worker before promotion finishes.
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
    Task<VersionDownload> GetFilVersioneDownloadDetails(Guid versionId, Guid userId, CancellationToken ct = default);

    Task StreamFile(
        Guid versionId,
        Stream destination,
        CancellationToken ct = default);

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

    /// <summary>
    /// Creates upload rows and signed URLs for a batch. Results line up with
    /// <paramref name="items"/>, and an unusable destination only fails its own items.
    /// </summary>
    Task<IReadOnlyList<UploadInitResult>> InitiateFileUploads(
        IReadOnlyList<UploadInitItem> items,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Claims the upload, seals its bytes where no signed URL can write, and checks the
    /// sealed copy. Holds no database transaction, so it can run in parallel across scopes.
    /// A failure here abandons the upload.
    /// </summary>
    Task<VerifiedUpload> VerifyUploadAsync(
        Guid uploadId,
        Guid uploadedBy,
        Guid? directoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Records a verified upload in one short transaction. A failure leaves the sealed bytes
    /// in place so the caller can retry in a fresh scope or call <see cref="AbandonUploadAsync"/>.
    /// </summary>
    Task<UploadResult> CommitUploadAsync(
        VerifiedUpload verified,
        UploadFinalizeItem item,
        CancellationToken ct = default);

    /// <summary>Ends a claimed upload as interrupted and removes its temp objects.</summary>
    Task AbandonUploadAsync(Guid uploadId, string tempObjectKey, CancellationToken ct = default);

    Task<StorageBreakdown> GetStorageBreakdown(Guid userId, CancellationToken ct = default);

    Task<string> GetVersionPresignedUrl(Guid fileVersionId, Guid ownerId, CancellationToken ct = default);

    Task StreamBulkZipAsync(
        Guid[] directoryIds,
        Guid[] fileIds,
        Guid userId,
        Stream destination,
        CancellationToken ct = default);
}