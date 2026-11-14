using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Previews;

namespace Alexandria.Common.Services;

/// <summary>
/// Owns <c>PreviewBucket</c> derived artifacts: preview/thumbnail upload, cache lookup,
/// deletion, listing.
/// </summary>
public interface IPreviewArtifactService
{
    Task UploadPreview(
        string objectName,
        string contentType,
        Stream fileStream,
        Guid versionId,
        Guid uploadedBy,
        long contentLength,
        PreviewKind kind,
        CancellationToken ct = default);

    /// <summary>
    /// Stores media metadata without preview artifacts, for installations with media previews off.
    /// </summary>
    Task SaveMediaMetadataAsync(Guid versionId, MediaMetadataDto metadata, CancellationToken ct = default);

    Task UploadMediaData(Stream previewStream,
        Stream thumbnailStream,
        long previewSize,
        long thumbnailSize,
        string objectName,
        Guid versionId,
        MediaMetadataDto metadataDto,
        CancellationToken ct = default);

    Task<string?> GetCachedPreview(Guid versionId, PreviewKind kind, CancellationToken ct = default);

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

    /// <summary>
    /// Sets an uploaded image as one audio version's cover. Automated generation leaves it
    /// alone from then on. Returns the cover's timestamp, which clients use as a cache-buster.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Version is missing or not owned by the user.</exception>
    /// <exception cref="InvalidOperationException">Version or image is not eligible.</exception>
    Task<DateTime> SetCustomCoverAsync(
        Guid fileId, Guid versionId, Guid userId, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Removes a version's manual cover, if it has one. The automatic thumbnail regenerates
    /// on the next read.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Version is missing or not owned by the user.</exception>
    Task RemoveCustomCoverAsync(Guid fileId, Guid versionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Object key to serve for a version's thumbnail: its manual cover if it has one,
    /// otherwise the generated thumbnail shared by its content hash.
    /// </summary>
    Task<string> GetThumbnailContentPathAsync(Guid versionId, string contentHash, CancellationToken ct = default);

    /// <summary>Cache-busting token of a version's manual cover, or null when the cover is automatic.</summary>
    Task<string?> GetCoverStampAsync(Guid versionId, CancellationToken ct = default);
}